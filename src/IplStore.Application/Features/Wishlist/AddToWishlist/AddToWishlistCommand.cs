using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Entities;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Wishlist.AddToWishlist;

public sealed record AddToWishlistCommand(Guid ProductId) : IRequest<Result>;

public sealed class AddToWishlistCommandHandler : IRequestHandler<AddToWishlistCommand, Result>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AddToWishlistCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(AddToWishlistCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } customerId)
            return Result.Failure(DomainErrors.Auth.InvalidCredentials);

        var productExists = await _db.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken);
        if (!productExists) return Result.Failure(DomainErrors.Product.NotFound);

        var already = await _db.WishlistItems
            .AnyAsync(w => w.CustomerId == customerId && w.ProductId == request.ProductId, cancellationToken);
        if (already) return Result.Success(); // idempotent add

        _db.WishlistItems.Add(WishlistItem.Create(customerId, request.ProductId));
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
