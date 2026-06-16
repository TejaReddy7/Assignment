using IplStore.Application.Common;
using IplStore.Application.Common.Abstractions;
using IplStore.Application.Common.Behaviors;
using IplStore.Domain.Enums;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Catalog.Featured;

/// <summary>
/// Returns the "Featured / Trending" rail for the storefront: the highest-demand products,
/// with a guarantee that every product category is represented at least once. Cached briefly
/// (invalidated whenever an order or review changes via the shared product cache prefix).
/// </summary>
public sealed record GetFeaturedProductsQuery(int Count = 10)
    : IRequest<Result<IReadOnlyList<FeaturedProductDto>>>, ICacheableQuery
{
    public string CacheKey => $"{CacheKeys.ProductPrefix}featured:{Count}";
    public TimeSpan? CacheDuration => TimeSpan.FromSeconds(120);
}

public sealed class GetFeaturedProductsQueryHandler
    : IRequestHandler<GetFeaturedProductsQuery, Result<IReadOnlyList<FeaturedProductDto>>>
{
    // A sale this many days old counts for half of a sale made today (exponential decay).
    private const double SalesHalfLifeDays = 30.0;
    // Products created within this window get a decaying "new arrival" boost.
    private const double NewnessWindowDays = 21.0;

    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetFeaturedProductsQueryHandler(IAppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<FeaturedProductDto>>> Handle(
        GetFeaturedProductsQuery request, CancellationToken cancellationToken)
    {
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Include(p => p.Franchise)
            .Include(p => p.Variants)
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
            return Result.Success<IReadOnlyList<FeaturedProductDto>>(Array.Empty<FeaturedProductDto>());

        // Pull raw sales rows for non-cancelled orders, then aggregate in memory.
        // (Projecting scalars first keeps the query provider-agnostic — same pattern as search facets.)
        var salesRows = await (
            from oi in _db.OrderItems
            join o in _db.Orders on oi.OrderId equals o.Id
            where o.Status != OrderStatus.Cancelled
            select new { oi.ProductId, oi.Quantity, o.Id, o.PlacedAtUtc })
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;

        var salesByProduct = salesRows
            .GroupBy(r => r.ProductId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Units = g.Sum(r => r.Quantity),
                    DistinctOrders = g.Select(r => r.Id).Distinct().Count(),
                    RecencyWeighted = g.Sum(r => r.Quantity * Decay(now, r.PlacedAtUtc)),
                });

        var signals = products
            .Select(p =>
            {
                salesByProduct.TryGetValue(p.Id, out var sales);
                return new ProductDemandSignal(
                    ProductId: p.Id,
                    Type: p.Type,
                    UnitsSold: sales?.Units ?? 0,
                    RecencyWeightedSales: sales?.RecencyWeighted ?? 0,
                    DistinctOrders: sales?.DistinctOrders ?? 0,
                    AverageRating: p.AverageRating,
                    ReviewCount: p.ReviewCount,
                    NewnessBoost: Newness(now, p.CreatedAtUtc),
                    InStock: p.Variants.Any(v => v.StockQuantity > 0));
            })
            .ToList();

        var ranked = FeaturedProductsRanker.Rank(signals, request.Count);

        var productById = products.ToDictionary(p => p.Id);
        var result = ranked
            .Select(r =>
            {
                var (badge, reason) = Describe(r.Reason);
                return new FeaturedProductDto(badge, reason, r.Score, productById[r.ProductId].ToListItemDto());
            })
            .ToList();

        return Result.Success<IReadOnlyList<FeaturedProductDto>>(result);
    }

    private static double Decay(DateTime now, DateTime placedAtUtc)
    {
        var ageDays = Math.Max(0, (now - placedAtUtc).TotalDays);
        return Math.Pow(0.5, ageDays / SalesHalfLifeDays);
    }

    private static double Newness(DateTime now, DateTime createdAtUtc)
    {
        var ageDays = Math.Max(0, (now - createdAtUtc).TotalDays);
        return Math.Max(0, 1.0 - (ageDays / NewnessWindowDays));
    }

    private static (string Badge, string Reason) Describe(FeaturedReason reason) => reason switch
    {
        FeaturedReason.Bestseller => ("Bestseller", "Most ordered by fans"),
        FeaturedReason.Trending => ("Trending", "Selling fast right now"),
        FeaturedReason.TopRated => ("Top Rated", "Loved by verified buyers"),
        FeaturedReason.NewArrival => ("New Arrival", "Fresh in the store"),
        _ => ("Discover", "Explore this category"),
    };
}
