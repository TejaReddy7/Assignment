using IplStore.Domain.Enums;
using IplStore.Domain.Errors;
using IplStore.Domain.Events;
using IplStore.Domain.Primitives;
using IplStore.Domain.ValueObjects;
using IplStore.Shared;

namespace IplStore.Domain.Entities;

public sealed class Order : Entity<Guid>, IAggregateRoot, IAuditable
{
    private readonly List<OrderItem> _items = new();

    private Order() { } // EF

    private Order(
        Guid id,
        string orderNumber,
        Guid customerId,
        Address shippingAddress,
        PaymentMethod paymentMethod,
        Money subtotal,
        Money discountAmount,
        Money shippingFee,
        Money total,
        string? couponCode,
        string idempotencyKey)
        : base(id)
    {
        OrderNumber = orderNumber;
        CustomerId = customerId;
        ShippingAddress = shippingAddress;
        PaymentMethod = paymentMethod;
        Subtotal = subtotal;
        DiscountAmount = discountAmount;
        ShippingFee = shippingFee;
        Total = total;
        CouponCode = couponCode;
        IdempotencyKey = idempotencyKey;
        Status = OrderStatus.Pending;
        PaymentStatus = PaymentStatus.Pending;
        PlacedAtUtc = DateTime.UtcNow;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public string OrderNumber { get; private set; } = default!;
    public Guid CustomerId { get; private set; }
    public Address ShippingAddress { get; private set; } = default!;
    public PaymentMethod PaymentMethod { get; private set; }
    public Money Subtotal { get; private set; }
    public Money DiscountAmount { get; private set; }
    public Money ShippingFee { get; private set; }
    public Money Total { get; private set; }
    public string? CouponCode { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public OrderStatus Status { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }
    public string? PaymentTransactionId { get; private set; }
    public DateTime PlacedAtUtc { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime? ShippedAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public static Result<Order> Place(
        Guid customerId,
        Address shippingAddress,
        PaymentMethod paymentMethod,
        IReadOnlyList<OrderItem> items,
        Money shippingFee,
        Money discountAmount,
        string? couponCode,
        string idempotencyKey,
        Func<string> orderNumberFactory)
    {
        if (items.Count == 0) return DomainErrors.Cart.Empty;
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Error.Validation("order.idempotency_required", "Idempotency-Key header is required.");

        var subtotal = items.Select(i => i.LineTotal).Aggregate((a, b) => a + b);
        if (discountAmount > subtotal) discountAmount = subtotal;
        var total = subtotal - discountAmount + shippingFee;

        var order = new Order(Guid.NewGuid(), orderNumberFactory(), customerId, shippingAddress, paymentMethod,
            subtotal, discountAmount, shippingFee, total, couponCode, idempotencyKey);
        order._items.AddRange(items);
        order.RaiseDomainEvent(new OrderPlacedEvent(order.Id, order.OrderNumber, customerId, total));
        return order;
    }

    public Result Confirm(string paymentTransactionId)
    {
        if (Status != OrderStatus.Pending) return Result.Failure(DomainErrors.Order.InvalidStateTransition);
        Status = OrderStatus.Confirmed;
        PaymentStatus = PaymentStatus.Captured;
        PaymentTransactionId = paymentTransactionId;
        ConfirmedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Ship()
    {
        if (Status != OrderStatus.Confirmed) return Result.Failure(DomainErrors.Order.InvalidStateTransition);
        Status = OrderStatus.Shipped;
        ShippedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    public Result MarkDelivered()
    {
        if (Status != OrderStatus.Shipped) return Result.Failure(DomainErrors.Order.InvalidStateTransition);
        Status = OrderStatus.Delivered;
        DeliveredAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status is OrderStatus.Shipped or OrderStatus.Delivered or OrderStatus.Cancelled)
            return Result.Failure(DomainErrors.Order.CannotCancel);

        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        CancelledAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        PaymentStatus = PaymentStatus == PaymentStatus.Captured ? PaymentStatus.Refunded : PaymentStatus.Failed;
        RaiseDomainEvent(new OrderCancelledEvent(Id, OrderNumber, CustomerId,
            _items.Select(i => (i.ProductVariantId, i.Quantity)).ToList()));
        return Result.Success();
    }
}
