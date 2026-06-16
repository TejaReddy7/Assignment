using FluentValidation;
using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Cart.AddCartItem;

public sealed record AddCartItemCommand(Guid ProductVariantId, int Quantity) : IRequest<Result<CartDto>>;

public sealed class AddCartItemCommandValidator : AbstractValidator<AddCartItemCommand>
{
    public AddCartItemCommandValidator()
    {
        RuleFor(x => x.ProductVariantId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(Domain.Entities.Cart.MaxQtyPerLine);
    }
}

public sealed class AddCartItemCommandHandler : IRequestHandler<AddCartItemCommand, Result<CartDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AddCartItemCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<CartDto>> Handle(AddCartItemCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } customerId)
            return DomainErrors.Auth.InvalidCredentials;

        // Variant + product are read-only reference data here (we only read price/stock/name),
        // so load them untracked. Tracking them would let EF spuriously mark the variant Modified
        // (nullable Money complex property), triggering an unintended optimistic-concurrency UPDATE.
        var variant = await _db.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == request.ProductVariantId, cancellationToken);

        if (variant is null) return DomainErrors.Variant.NotFound;
        if (!variant.Product.IsActive) return DomainErrors.Product.Inactive;

        var cart = await CartLoader.GetOrCreateAsync(_db, customerId, cancellationToken);

        var existingItemIds = cart.Items.Select(i => i.Id).ToHashSet();

        var unitPrice = variant.EffectivePrice(variant.Product.BasePrice);
        var add = cart.AddOrMerge(
            variant.ProductId,
            variant.Id,
            variant.Product.Name,
            variant.Product.ImageUrl,
            unitPrice,
            request.Quantity,
            variant.StockQuantity);

        if (add.IsFailure) return add.Error;

        // Our entities carry client-generated GUID keys, so EF can't infer "new" from the key
        // when a line is added to an already-tracked cart. Explicitly mark a brand-new line as
        // Added so EF emits an INSERT (not an UPDATE that would affect 0 rows).
        if (!existingItemIds.Contains(add.Value.Id))
            _db.CartItems.Add(add.Value);

        await _db.SaveChangesAsync(cancellationToken);
        return cart.ToDto();
    }
}
