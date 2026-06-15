using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Wishlist.RemoveFromWishlist;

public sealed record RemoveFromWishlistCommand(Guid ProductId) : IRequest<Result>;

public sealed class RemoveFromWishlistCommandHandler : IRequestHandler<RemoveFromWishlistCommand, Result>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public RemoveFromWishlistCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(RemoveFromWishlistCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } customerId)
            return Result.Failure(DomainErrors.Auth.InvalidCredentials);

        var item = await _db.WishlistItems
            .FirstOrDefaultAsync(w => w.CustomerId == customerId && w.ProductId == request.ProductId, cancellationToken);

        if (item is not null)
        {
            _db.WishlistItems.Remove(item);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(); // idempotent remove
    }
}
