using IplStore.Domain.Enums;

namespace IplStore.Application.Features.Catalog.Featured;

/// <summary>Why a product earned a spot in the featured rail. Drives the UI badge.</summary>
public enum FeaturedReason
{
    Bestseller,    // highest raw units sold all-time
    Trending,      // strong recency-weighted sales velocity
    TopRated,      // high average rating with enough reviews
    NewArrival,    // recently added to the catalog
    CategoryPick   // included to guarantee every category is represented
}

/// <summary>
/// Pre-computed demand inputs for a single product. The handler builds these from the
/// database; the ranker stays a pure function of them so it can be unit-tested in isolation.
/// </summary>
public sealed record ProductDemandSignal(
    Guid ProductId,
    ProductType Type,
    int UnitsSold,
    double RecencyWeightedSales,
    int DistinctOrders,
    decimal AverageRating,
    int ReviewCount,
    double NewnessBoost,   // 0..1, higher = newer
    bool InStock);

public sealed record FeaturedRanking(Guid ProductId, double Score, FeaturedReason Reason);

/// <summary>Tunable weights for the composite demand score (DI-overridable, defaulted sensibly).</summary>
public sealed record FeaturedRankingOptions
{
    public double SalesWeight { get; init; } = 10.0;
    public double OrdersWeight { get; init; } = 4.0;
    public double RatingWeight { get; init; } = 3.0;
    public double NewnessWeight { get; init; } = 2.0;
    public double StockWeight { get; init; } = 0.5;

    public static readonly FeaturedRankingOptions Default = new();
}

/// <summary>
/// Ranks products for the "Featured / Trending" rail using a transparent, weighted demand score
/// and then guarantees category diversity: every product category that exists in the catalog is
/// represented by at least one item, even if it has no sales yet. Remaining slots are filled by
/// raw demand. Deterministic and side-effect free.
/// </summary>
public static class FeaturedProductsRanker
{
    public static IReadOnlyList<FeaturedRanking> Rank(
        IReadOnlyList<ProductDemandSignal> signals,
        int count,
        FeaturedRankingOptions? options = null)
    {
        if (signals.Count == 0) return Array.Empty<FeaturedRanking>();

        var opts = options ?? FeaturedRankingOptions.Default;

        // 1. Score every product and remember the dominant signal for badge assignment.
        var bestSellerId = signals
            .Where(s => s.UnitsSold > 0)
            .OrderByDescending(s => s.UnitsSold)
            .Select(s => (Guid?)s.ProductId)
            .FirstOrDefault();

        var scored = signals
            .Select(s => Score(s, opts, bestSellerId))
            .ToList();

        // Coverage requires at least one slot per distinct category, so raise count if needed.
        var distinctTypes = signals.Select(s => s.Type).Distinct().ToList();
        var targetCount = Math.Min(signals.Count, Math.Max(count, distinctTypes.Count));

        // 2. Coverage pass — take the best-scoring product of each category first.
        var selected = new List<ScoredProduct>();
        var selectedIds = new HashSet<Guid>();

        var bestPerType = distinctTypes
            .Select(type => scored.Where(s => s.Signal.Type == type).OrderByDescending(s => s.Score).First())
            .OrderByDescending(s => s.Score) // strongest categories lead
            .ToList();

        foreach (var pick in bestPerType)
        {
            if (selected.Count >= targetCount) break;
            if (selectedIds.Add(pick.Signal.ProductId))
                selected.Add(pick with { IsCoveragePick = true });
        }

        // 3. Fill pass — add the remaining highest-demand products by score.
        foreach (var candidate in scored.OrderByDescending(s => s.Score))
        {
            if (selected.Count >= targetCount) break;
            if (selectedIds.Add(candidate.Signal.ProductId))
                selected.Add(candidate);
        }

        // 4. Final display order = demand score desc (coverage already guaranteed).
        return selected
            .OrderByDescending(s => s.Score)
            .Select(s => new FeaturedRanking(s.Signal.ProductId, Math.Round(s.Score, 4), ResolveReason(s)))
            .ToList();
    }

    private static ScoredProduct Score(ProductDemandSignal s, FeaturedRankingOptions o, Guid? bestSellerId)
    {
        // Log dampening keeps a runaway bestseller from dwarfing everything else.
        var salesComponent = o.SalesWeight * Math.Log(1 + s.RecencyWeightedSales);
        var ordersComponent = o.OrdersWeight * Math.Log(1 + s.DistinctOrders);
        var ratingComponent = o.RatingWeight * ((double)s.AverageRating / 5.0) * Math.Log(1 + s.ReviewCount);
        var newnessComponent = o.NewnessWeight * s.NewnessBoost;
        var stockComponent = o.StockWeight * (s.InStock ? 1.0 : 0.0);

        var total = salesComponent + ordersComponent + ratingComponent + newnessComponent + stockComponent;

        return new ScoredProduct(s, total, salesComponent, ratingComponent, newnessComponent,
            IsBestSeller: bestSellerId == s.ProductId);
    }

    private static FeaturedReason ResolveReason(ScoredProduct s)
    {
        if (s.IsBestSeller && s.Signal.UnitsSold > 0) return FeaturedReason.Bestseller;

        // A coverage pick with no real demand signal is surfaced purely for discovery.
        var hasDemand = s.Signal.RecencyWeightedSales > 0 || s.Signal.ReviewCount > 0;
        if (s.IsCoveragePick && !hasDemand) return FeaturedReason.CategoryPick;

        var maxComponent = Math.Max(s.SalesComponent, Math.Max(s.RatingComponent, s.NewnessComponent));

        if (s.SalesComponent > 0 && s.SalesComponent >= maxComponent) return FeaturedReason.Trending;
        if (s.RatingComponent > 0 && s.RatingComponent >= maxComponent) return FeaturedReason.TopRated;
        if (s.NewnessComponent > 0 && s.NewnessComponent >= maxComponent) return FeaturedReason.NewArrival;

        return FeaturedReason.CategoryPick;
    }

    private sealed record ScoredProduct(
        ProductDemandSignal Signal,
        double Score,
        double SalesComponent,
        double RatingComponent,
        double NewnessComponent,
        bool IsBestSeller = false,
        bool IsCoveragePick = false);
}
