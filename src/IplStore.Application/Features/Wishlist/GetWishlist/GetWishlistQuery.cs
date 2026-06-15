using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Wishlist.GetWishlist;

public sealed record GetWishlistQuery : IRequest<Result<IReadOnlyList<WishlistItemDto>>>;

public sealed class GetWishlistQueryHandler
    : IRequestHandler<GetWishlistQuery, Result<IReadOnlyList<WishlistItemDto>>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetWishlistQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<WishlistItemDto>>> Handle(
        GetWishlistQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } customerId)
            return DomainErrors.Auth.InvalidCredentials;

        var items = await _db.WishlistItems
            .AsNoTracking()
            .Where(w => w.CustomerId == customerId)
            .OrderByDescending(w => w.AddedAtUtc)
            .Join(_db.Products.Include(p => p.Franchise).Include(p => p.Variants),
                w => w.ProductId,
                p => p.Id,
                (w, p) => new WishlistItemDto(
                    p.Id,
                    p.Name,
                    p.Slug.Value,
                    p.BasePrice.Amount,
                    p.BasePrice.Currency,
                    p.ImageUrl,
                    p.Franchise.ShortCode,
                    p.Variants.Any(v => v.StockQuantity > 0),
                    w.AddedAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success((IReadOnlyList<WishlistItemDto>)items);
    }
}
