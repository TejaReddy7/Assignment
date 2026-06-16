using IplStore.Application.Features.Catalog.Featured;
using IplStore.Domain.Enums;

namespace IplStore.Application.Tests.Features;

public class FeaturedProductsRankerTests
{
    private static ProductDemandSignal Signal(
        ProductType type,
        int units = 0,
        double recencyWeighted = 0,
        int distinctOrders = 0,
        decimal rating = 0,
        int reviews = 0,
        double newness = 0,
        bool inStock = true,
        Guid? id = null)
        => new(id ?? Guid.NewGuid(), type, units, recencyWeighted, distinctOrders, rating, reviews, newness, inStock);

    [Fact]
    public void Rank_EmptyInput_ReturnsEmpty()
    {
        var result = FeaturedProductsRanker.Rank(Array.Empty<ProductDemandSignal>(), 10);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Rank_GuaranteesEveryCategoryAppears_EvenWithNoDemand()
    {
        // One product per category, all with zero sales/reviews — pure discovery scenario.
        var signals = new[]
        {
            Signal(ProductType.Jersey, newness: 1),
            Signal(ProductType.Cap, newness: 1),
            Signal(ProductType.Flag, newness: 1),
            Signal(ProductType.AutographedPhoto, newness: 1),
            Signal(ProductType.Accessory, newness: 1),
            Signal(ProductType.Memorabilia, newness: 1),
        };

        var result = FeaturedProductsRanker.Rank(signals, count: 10);

        var coveredTypes = result
            .Join(signals, r => r.ProductId, s => s.ProductId, (_, s) => s.Type)
            .Distinct()
            .ToList();

        coveredTypes.Should().HaveCount(6);
    }

    [Fact]
    public void Rank_WhenCountSmallerThanCategories_StillCoversEveryCategory()
    {
        var signals = new[]
        {
            Signal(ProductType.Jersey, recencyWeighted: 50),
            Signal(ProductType.Cap, recencyWeighted: 5),
            Signal(ProductType.Flag, recencyWeighted: 1),
        };

        // Asking for 1, but coverage forces at least one per distinct category (3).
        var result = FeaturedProductsRanker.Rank(signals, count: 1);

        result.Should().HaveCount(3);
    }

    [Fact]
    public void Rank_OrdersByDemandScore_HighestFirst()
    {
        var hot = Signal(ProductType.Jersey, units: 100, recencyWeighted: 90, distinctOrders: 40, id: Guid.NewGuid());
        var warm = Signal(ProductType.Jersey, units: 10, recencyWeighted: 8, distinctOrders: 5, id: Guid.NewGuid());
        var cold = Signal(ProductType.Jersey, newness: 0.1, id: Guid.NewGuid());

        var result = FeaturedProductsRanker.Rank(new[] { cold, warm, hot }, count: 3);

        result[0].ProductId.Should().Be(hot.ProductId);
        result[0].Score.Should().BeGreaterThan(result[1].Score);
        result[1].Score.Should().BeGreaterThan(result[2].Score);
    }

    [Fact]
    public void Rank_TopRawSeller_GetsBestsellerBadge()
    {
        var bestseller = Signal(ProductType.Jersey, units: 200, recencyWeighted: 20, id: Guid.NewGuid());
        var recent = Signal(ProductType.Cap, units: 30, recencyWeighted: 28, id: Guid.NewGuid());

        var result = FeaturedProductsRanker.Rank(new[] { bestseller, recent }, count: 5);

        result.Single(r => r.ProductId == bestseller.ProductId).Reason.Should().Be(FeaturedReason.Bestseller);
    }

    [Fact]
    public void Rank_RecentVelocityNonTopSeller_GetsTrendingBadge()
    {
        // Old bestseller (lots of units long ago) vs a fresh fast-mover (fewer units, all recent).
        var oldBestseller = Signal(ProductType.Jersey, units: 200, recencyWeighted: 5, id: Guid.NewGuid());
        var trending = Signal(ProductType.Jersey, units: 60, recencyWeighted: 55, id: Guid.NewGuid());

        var result = FeaturedProductsRanker.Rank(new[] { oldBestseller, trending }, count: 5);

        result.Single(r => r.ProductId == oldBestseller.ProductId).Reason.Should().Be(FeaturedReason.Bestseller);
        result.Single(r => r.ProductId == trending.ProductId).Reason.Should().Be(FeaturedReason.Trending);
    }

    [Fact]
    public void Rank_HighlyRatedNoSales_GetsTopRatedBadge()
    {
        // Two jerseys so the rated one is a fill (not a forced coverage pick), isolating the badge logic.
        var established = Signal(ProductType.Jersey, recencyWeighted: 30, id: Guid.NewGuid());
        var topRated = Signal(ProductType.Jersey, rating: 4.9m, reviews: 80, newness: 0, id: Guid.NewGuid());

        var result = FeaturedProductsRanker.Rank(new[] { established, topRated }, count: 5);

        result.Single(r => r.ProductId == topRated.ProductId).Reason.Should().Be(FeaturedReason.TopRated);
    }

    [Fact]
    public void Rank_BrandNewNoSalesNoReviews_GetsNewArrivalBadge()
    {
        var established = Signal(ProductType.Jersey, rating: 5m, reviews: 50, newness: 0, id: Guid.NewGuid());
        var brandNew = Signal(ProductType.Jersey, newness: 1.0, id: Guid.NewGuid());

        var result = FeaturedProductsRanker.Rank(new[] { established, brandNew }, count: 5);

        result.Single(r => r.ProductId == brandNew.ProductId).Reason.Should().Be(FeaturedReason.NewArrival);
    }

    [Fact]
    public void Rank_SoleCategoryRepresentativeWithNoDemand_GetsCategoryPickBadge()
    {
        var jersey = Signal(ProductType.Jersey, recencyWeighted: 40, id: Guid.NewGuid());
        // Only flag in the catalog, no sales, no reviews, not new → surfaced purely for discovery.
        var lonelyFlag = Signal(ProductType.Flag, newness: 0, id: Guid.NewGuid());

        var result = FeaturedProductsRanker.Rank(new[] { jersey, lonelyFlag }, count: 5);

        result.Single(r => r.ProductId == lonelyFlag.ProductId).Reason.Should().Be(FeaturedReason.CategoryPick);
    }

    [Fact]
    public void Rank_NeverExceedsAvailableProducts()
    {
        var signals = new[] { Signal(ProductType.Jersey), Signal(ProductType.Cap) };

        var result = FeaturedProductsRanker.Rank(signals, count: 50);

        result.Should().HaveCount(2);
    }
}
