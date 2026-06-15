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

        var variant = await _db.ProductVariants
            .Include(v => v.Product)
            .ThenInclude(p => p.Franchise)
            .FirstOrDefaultAsync(v => v.Id == request.ProductVariantId, cancellationToken);

        if (variant is null) return DomainErrors.Variant.NotFound;
        if (!variant.Product.IsActive) return DomainErrors.Product.Inactive;

        var cart = await CartLoader.GetOrCreateAsync(_db, customerId, cancellationToken);

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

        await _db.SaveChangesAsync(cancellationToken);
        return cart.ToDto();
    }
}
