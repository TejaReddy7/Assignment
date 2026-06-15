using FluentValidation;
using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Cart.UpdateCartItem;

public sealed record UpdateCartItemCommand(Guid ItemId, int Quantity) : IRequest<Result<CartDto>>;

public sealed class UpdateCartItemCommandValidator : AbstractValidator<UpdateCartItemCommand>
{
    public UpdateCartItemCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).LessThanOrEqualTo(Domain.Entities.Cart.MaxQtyPerLine);
    }
}

public sealed class UpdateCartItemCommandHandler : IRequestHandler<UpdateCartItemCommand, Result<CartDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateCartItemCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<CartDto>> Handle(UpdateCartItemCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } customerId)
            return DomainErrors.Auth.InvalidCredentials;

        var cart = await CartLoader.GetOrCreateAsync(_db, customerId, cancellationToken);

        var item = cart.Items.FirstOrDefault(i => i.Id == request.ItemId);
        if (item is null) return DomainErrors.Cart.ItemNotFound;

        // Re-check stock for the target variant before increasing quantity.
        var stock = await _db.ProductVariants
            .Where(v => v.Id == item.ProductVariantId)
            .Select(v => v.StockQuantity)
            .FirstOrDefaultAsync(cancellationToken);

        var update = cart.UpdateQuantity(request.ItemId, request.Quantity, stock);
        if (update.IsFailure) return update.Error;

        await _db.SaveChangesAsync(cancellationToken);
        return cart.ToDto();
    }
}
