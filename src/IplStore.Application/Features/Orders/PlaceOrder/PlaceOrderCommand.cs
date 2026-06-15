using FluentValidation;
using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Entities;
using IplStore.Domain.Enums;
using IplStore.Domain.Errors;
using IplStore.Domain.ValueObjects;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IplStore.Application.Features.Orders.PlaceOrder;

public sealed record PlaceOrderCommand(
    AddressDto ShippingAddress,
    PaymentMethod PaymentMethod,
    string? CouponCode,
    string IdempotencyKey) : IRequest<Result<OrderDetailsDto>>;

public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().WithMessage("Idempotency-Key header is required.");
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.ShippingAddress).NotNull();
        RuleFor(x => x.ShippingAddress.Line1).NotEmpty();
        RuleFor(x => x.ShippingAddress.City).NotEmpty();
        RuleFor(x => x.ShippingAddress.State).NotEmpty();
        RuleFor(x => x.ShippingAddress.PostalCode).NotEmpty();
        RuleFor(x => x.ShippingAddress.Country).NotEmpty();
    }
}

public sealed class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Result<OrderDetailsDto>>
{
    // Flat shipping, waived for larger baskets — a small, realistic business rule.
    private static readonly Money FlatShippingFee = Money.From(99m);
    private static readonly Money FreeShippingThreshold = Money.From(4999m);

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IOrderNumberGenerator _orderNumbers;
    private readonly ICacheService _cache;
    private readonly ILogger<PlaceOrderCommandHandler> _logger;

    public PlaceOrderCommandHandler(
        IAppDbContext db,
        ICurrentUser currentUser,
        IPaymentGateway paymentGateway,
        IOrderNumberGenerator orderNumbers,
        ICacheService cache,
        ILogger<PlaceOrderCommandHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _paymentGateway = paymentGateway;
        _orderNumbers = orderNumbers;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<OrderDetailsDto>> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } customerId)
            return DomainErrors.Auth.InvalidCredentials;

        // 1. Idempotency: a retried request with the same key returns the original order.
        var existing = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.IdempotencyKey == request.IdempotencyKey,
                cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation("Idempotency replay for key {Key} → order {OrderNumber}.",
                request.IdempotencyKey, existing.OrderNumber);
            return existing.ToDetailsDto();
        }

        // 2. Validate shipping address value object.
        var addressResult = Address.Create(
            request.ShippingAddress.Line1, request.ShippingAddress.Line2, request.ShippingAddress.City,
            request.ShippingAddress.State, request.ShippingAddress.PostalCode, request.ShippingAddress.Country);
        if (addressResult.IsFailure) return addressResult.Error;

        // 3. Load the cart with items.
        var cart = await _db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
            return DomainErrors.Cart.Empty;

        // 4. Load the live variants (tracked) for stock + current pricing.
        var variantIds = cart.Items.Select(i => i.ProductVariantId).ToList();
        var variants = await _db.ProductVariants
            .Include(v => v.Product)
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        // 5. Build order items from snapshots of the CURRENT price + validate stock.
        var orderItems = new List<OrderItem>();
        foreach (var cartItem in cart.Items)
        {
            if (!variants.TryGetValue(cartItem.ProductVariantId, out var variant))
                return DomainErrors.Variant.NotFound;
            if (!variant.Product.IsActive)
                return DomainErrors.Product.Inactive;
            if (cartItem.Quantity > variant.StockQuantity)
                return DomainErrors.Variant.InsufficientStock;

            var unitPrice = variant.EffectivePrice(variant.Product.BasePrice);
            var itemResult = OrderItem.Create(
                variant.ProductId, variant.Id, variant.Product.Name, variant.Sku, unitPrice, cartItem.Quantity);
            if (itemResult.IsFailure) return itemResult.Error;
            orderItems.Add(itemResult.Value);
        }

        // 6. Apply coupon (if any) against the computed subtotal.
        var subtotal = orderItems.Select(i => i.LineTotal).Aggregate((a, b) => a + b);
        var discount = Money.Zero;
        Coupon? coupon = null;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var code = request.CouponCode.Trim().ToUpperInvariant();
            coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
            if (coupon is null) return DomainErrors.Coupon.NotFound;

            var discountResult = coupon.ComputeDiscount(subtotal);
            if (discountResult.IsFailure) return discountResult.Error;
            discount = discountResult.Value;
        }

        var shippingFee = (subtotal - discount) >= FreeShippingThreshold ? Money.Zero : FlatShippingFee;

        // 7. Single transaction: decrement stock, create order, charge, confirm, clear cart.
        // Stock decrement + order insert share one transaction so we never oversell.
        // (Production note: for very high contention, move payment outside the DB transaction
        //  using a reservation + outbox/saga; mock gateway keeps this single-transaction here.)
        try
        {
            var order = await _db.ExecuteInTransactionAsync(async ct =>
            {
                foreach (var cartItem in cart.Items)
                {
                    var variant = variants[cartItem.ProductVariantId];
                    var decrement = variant.Decrement(cartItem.Quantity);
                    if (decrement.IsFailure)
                        throw new StockConflictException();
                }

                var orderResult = Order.Place(
                    customerId, addressResult.Value, request.PaymentMethod, orderItems,
                    shippingFee, discount, coupon?.Code, request.IdempotencyKey, _orderNumbers.Next);
                if (orderResult.IsFailure)
                    throw new DomainPlacementException(orderResult.Error);

                var placedOrder = orderResult.Value;

                // Charge the (mock) gateway.
                var payment = await _paymentGateway.ChargeAsync(
                    new PaymentRequest(placedOrder.Id, placedOrder.OrderNumber,
                        placedOrder.Total.Amount, placedOrder.Total.Currency, request.PaymentMethod), ct);
                if (!payment.Success)
                    throw new DomainPlacementException(DomainErrors.Order.PaymentFailed);

                placedOrder.Confirm(payment.TransactionId!);

                _db.Orders.Add(placedOrder);
                coupon?.IncrementUsage();
                cart.Clear();

                await _db.SaveChangesAsync(ct);
                return placedOrder;
            }, cancellationToken);

            _logger.LogInformation("Order {OrderNumber} placed for customer {CustomerId}.",
                order.OrderNumber, customerId);

            // Stock changed, so product reads are now stale — invalidate the catalog cache.
            await _cache.RemoveByPrefixAsync(Common.CacheKeys.ProductPrefix, cancellationToken);

            return order.ToDetailsDto();
        }
        catch (StockConflictException)
        {
            return DomainErrors.Variant.InsufficientStock;
        }
        catch (DomainPlacementException ex)
        {
            return ex.Error;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another order grabbed the last unit between our read and write.
            return DomainErrors.Variant.StockChanged;
        }
    }

    private sealed class StockConflictException : Exception;

    private sealed class DomainPlacementException(Error error) : Exception
    {
        public Error Error { get; } = error;
    }
}
