using IplStore.Domain.Enums;

namespace IplStore.Application.Features.Catalog.Search;

public sealed record FacetCount(string Value, string Label, int Count);

public sealed record PriceBucket(string Label, decimal Min, decimal? Max, int Count);

public sealed record SearchFacets(
    IReadOnlyList<FacetCount> Franchises,
    IReadOnlyList<FacetCount> Types,
    IReadOnlyList<PriceBucket> PriceBuckets);

public sealed record SearchResult(
    IReadOnlyList<ProductListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    SearchFacets Facets)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
