using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;

namespace IplStore.Application.Features.Cart.RemoveCartItem;

public sealed record RemoveCartItemCommand(Guid ItemId) : IRequest<Result<CartDto>>;

public sealed class RemoveCartItemCommandHandler : IRequestHandler<RemoveCartItemCommand, Result<CartDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public RemoveCartItemCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<CartDto>> Handle(RemoveCartItemCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } customerId)
            return DomainErrors.Auth.InvalidCredentials;

        var cart = await CartLoader.GetOrCreateAsync(_db, customerId, cancellationToken);

        var remove = cart.Remove(request.ItemId);
        if (remove.IsFailure) return remove.Error;

        await _db.SaveChangesAsync(cancellationToken);
        return cart.ToDto();
    }
}
