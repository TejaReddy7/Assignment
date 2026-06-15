using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Entities;
using IplStore.Domain.Enums;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Catalog.Search;

/// <summary>
/// Faceted product search by free text (name/description), franchise, type, and price range.
/// Returns paginated results plus facet counts so a UI can render filter sidebars with numbers.
/// </summary>
public sealed record SearchProductsQuery(
    string? Q = null,
    string? Franchise = null,
    ProductType? Type = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool InStockOnly = false,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = "relevance",
    string? SortDir = "desc")
    : IRequest<Result<SearchResult>>;

public sealed class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, Result<SearchResult>>
{
    private readonly IAppDbContext _db;

    public SearchProductsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<Result<SearchResult>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var pagination = new PaginationParams { Page = request.Page, PageSize = request.PageSize };

        // Base query: active products with their franchise + variants.
        var baseQuery = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Include(p => p.Franchise)
            .Include(p => p.Variants)
            .AsQueryable();

        baseQuery = ApplyTextFilter(baseQuery, request.Q);

        // Facets are computed BEFORE the franchise/type/price filters so the sidebar
        // can show counts for options the user hasn't selected yet (classic faceted UX).
        var facetSource = baseQuery;

        var filtered = ApplyFilters(baseQuery, request);

        var total = await filtered.CountAsync(cancellationToken);

        var ordered = ApplySort(filtered, request);

        var products = await ordered
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var items = products.Select(p => p.ToListItemDto()).ToList();
        var facets = await BuildFacetsAsync(facetSource, cancellationToken);

        return new SearchResult(items, pagination.Page, pagination.PageSize, total, facets);
    }

    private static IQueryable<Product> ApplyTextFilter(IQueryable<Product> query, string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return query;
        var term = q.Trim();
        // EF Core translates Contains to SQL LIKE — case-insensitive on SQLite/SQL Server defaults.
        return query.Where(p =>
            EF.Functions.Like(p.Name, $"%{term}%") ||
            EF.Functions.Like(p.Description, $"%{term}%") ||
            EF.Functions.Like(p.Franchise.Name, $"%{term}%"));
    }

    private static IQueryable<Product> ApplyFilters(IQueryable<Product> query, SearchProductsQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.Franchise))
        {
            var code = request.Franchise.Trim().ToUpperInvariant();
            query = query.Where(p => p.Franchise.ShortCode == code);
        }

        if (request.Type is { } type)
            query = query.Where(p => p.Type == type);

        if (request.MinPrice is { } min)
            query = query.Where(p => p.BasePrice.Amount >= min);

        if (request.MaxPrice is { } max)
            query = query.Where(p => p.BasePrice.Amount <= max);

        if (request.InStockOnly)
            query = query.Where(p => p.Variants.Any(v => v.StockQuantity > 0));

        return query;
    }

    private static IQueryable<Product> ApplySort(IQueryable<Product> query, SearchProductsQuery request)
    {
        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);

        switch (request.SortBy?.ToLowerInvariant())
        {
            case "price":
                return descending
                    ? query.OrderByDescending(p => p.BasePrice.Amount)
                    : query.OrderBy(p => p.BasePrice.Amount);
            case "rating":
                return query.OrderByDescending(p => p.AverageRating);
            case "name":
                return descending
                    ? query.OrderByDescending(p => p.Name)
                    : query.OrderBy(p => p.Name);
            default:
                // Relevance heuristic: when a query is present, rank exact/prefix name matches first.
                if (!string.IsNullOrWhiteSpace(request.Q))
                {
                    var term = request.Q.Trim();
                    return query
                        .OrderByDescending(p => p.Name == term)
                        .ThenByDescending(p => p.Name.StartsWith(term))
                        .ThenByDescending(p => p.AverageRating)
                        .ThenBy(p => p.Name);
                }
                return query.OrderByDescending(p => p.AverageRating).ThenBy(p => p.Name);
        }
    }

    private static async Task<SearchFacets> BuildFacetsAsync(IQueryable<Product> source, CancellationToken ct)
    {
        // Project to lightweight scalars server-side (translatable), then aggregate in memory.
        // Avoids EF Core's GroupBy-with-Include translation limits and keeps the payload small.
        var rows = await source
            .Select(p => new FacetRow(
                p.Franchise.ShortCode,
                p.Franchise.Name,
                p.Type,
                p.BasePrice.Amount))
            .ToListAsync(ct);

        var franchiseFacets = rows
            .GroupBy(r => new { r.FranchiseShortCode, r.FranchiseName })
            .Select(g => new FacetCount(g.Key.FranchiseShortCode, g.Key.FranchiseName, g.Count()))
            .OrderByDescending(f => f.Count)
            .ThenBy(f => f.Label)
            .ToList();

        var typeFacets = rows
            .GroupBy(r => r.Type)
            .Select(g => new FacetCount(g.Key.ToString(), Humanize(g.Key), g.Count()))
            .OrderByDescending(f => f.Count)
            .ToList();

        var prices = rows.Select(r => r.Price).ToList();
        var priceBuckets = new List<PriceBucket>
        {
            new("Under ₹500", 0, 500, prices.Count(a => a < 500)),
            new("₹500 - ₹1000", 500, 1000, prices.Count(a => a >= 500 && a < 1000)),
            new("₹1000 - ₹2000", 1000, 2000, prices.Count(a => a >= 1000 && a < 2000)),
            new("₹2000 & above", 2000, null, prices.Count(a => a >= 2000)),
        };

        return new SearchFacets(franchiseFacets, typeFacets, priceBuckets);
    }

    private sealed record FacetRow(string FranchiseShortCode, string FranchiseName, ProductType Type, decimal Price);

    private static string Humanize(ProductType type) => type switch
    {
        ProductType.AutographedPhoto => "Autographed Photo",
        _ => type.ToString()
    };
}
