using System.Linq.Expressions;
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
/// Supports fuzzy matching with plural/singular stemming and searches across:
/// Name, Description, Franchise Name, Franchise ShortCode, Franchise City, and Product Type.
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

    /// <summary>
    /// Enhanced text search with stemming/normalization.
    /// Generates plural/singular variants of each search token and matches against
    /// Name, Description, Franchise.Name, Franchise.ShortCode, Franchise.City, and Type (as string).
    /// Multi-word queries use AND logic between words (each word must match somewhere).
    /// </summary>
    private static IQueryable<Product> ApplyTextFilter(IQueryable<Product> query, string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return query;

        var rawTerms = q.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (rawTerms.Length == 0) return query;

        // For each word, generate stem variants (e.g., "Jerseys" → ["Jerseys", "Jersey"])
        // Then require ALL words to match somewhere (AND between words, OR between variants of each word).
        foreach (var rawWord in rawTerms)
        {
            var variants = GenerateStemVariants(rawWord);
            query = query.Where(BuildWordPredicate(variants));
        }

        return query;
    }

    /// <summary>
    /// Builds an OR predicate: the product matches if ANY variant of a single word
    /// appears in ANY of the searchable columns.
    /// </summary>
    private static Expression<Func<Product, bool>> BuildWordPredicate(List<string> variants)
    {
        var param = Expression.Parameter(typeof(Product), "p");
        Expression? combined = null;

        foreach (var variant in variants)
        {
            var pattern = $"%{variant}%";

            // Build: EF.Functions.Like(p.Name, pattern) || ...Like(p.Description, pattern) || ...
            var likeMethod = typeof(DbFunctionsExtensions).GetMethod(
                nameof(DbFunctionsExtensions.Like),
                new[] { typeof(DbFunctions), typeof(string), typeof(string) })!;

            var efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
            var patternExpr = Expression.Constant(pattern);

            // p.Name
            var nameExpr = Expression.Call(likeMethod, efFunctions,
                Expression.Property(param, nameof(Product.Name)), patternExpr);

            // p.Description
            var descExpr = Expression.Call(likeMethod, efFunctions,
                Expression.Property(param, nameof(Product.Description)), patternExpr);

            // p.Franchise.Name
            var franchiseProp = Expression.Property(param, nameof(Product.Franchise));
            var franchiseNameExpr = Expression.Call(likeMethod, efFunctions,
                Expression.Property(franchiseProp, nameof(Franchise.Name)), patternExpr);

            // p.Franchise.ShortCode
            var franchiseCodeExpr = Expression.Call(likeMethod, efFunctions,
                Expression.Property(franchiseProp, nameof(Franchise.ShortCode)), patternExpr);

            // p.Franchise.City
            var franchiseCityExpr = Expression.Call(likeMethod, efFunctions,
                Expression.Property(franchiseProp, nameof(Franchise.City)), patternExpr);

            // Combine all column matches with OR for this variant
            Expression variantMatch = nameExpr;
            variantMatch = Expression.OrElse(variantMatch, descExpr);
            variantMatch = Expression.OrElse(variantMatch, franchiseNameExpr);
            variantMatch = Expression.OrElse(variantMatch, franchiseCodeExpr);
            variantMatch = Expression.OrElse(variantMatch, franchiseCityExpr);

            // Also match against the ProductType column (stored as string in DB)
            // Check if variant matches any known ProductType name
            foreach (var pt in Enum.GetValues<ProductType>())
            {
                var typeName = pt.ToString();
                if (typeName.Contains(variant, StringComparison.OrdinalIgnoreCase) ||
                    variant.Contains(typeName, StringComparison.OrdinalIgnoreCase))
                {
                    // p.Type == ProductType.X
                    var typeMatch = Expression.Equal(
                        Expression.Property(param, nameof(Product.Type)),
                        Expression.Constant(pt));
                    variantMatch = Expression.OrElse(variantMatch, typeMatch);
                }
            }

            combined = combined is null ? variantMatch : Expression.OrElse(combined, variantMatch);
        }

        return Expression.Lambda<Func<Product, bool>>(combined!, param);
    }

    /// <summary>
    /// Generates simple stemming variants for a word to handle common English plural/singular forms.
    /// E.g. "Jerseys" → ["Jerseys", "Jersey"], "Cap" → ["Cap", "Caps"],
    ///      "Accessories" → ["Accessories", "Accessory", "Accessorie"].
    /// This covers the most common e-commerce search typos without needing a full NLP library.
    /// </summary>
    private static List<string> GenerateStemVariants(string word)
    {
        var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { word };
        var lower = word.ToLowerInvariant();

        // Strip plural → singular
        if (lower.EndsWith("ies") && lower.Length > 4)
        {
            // "accessories" → "accessory"
            variants.Add(word[..^3] + "y");
        }
        else if (lower.EndsWith("ves") && lower.Length > 4)
        {
            // "scarves" → "scarf"
            variants.Add(word[..^3] + "f");
        }
        else if (lower.EndsWith("ses") || lower.EndsWith("xes") || lower.EndsWith("zes") ||
                 lower.EndsWith("ches") || lower.EndsWith("shes"))
        {
            // "matches" → "match", "boxes" → "box"
            if (lower.EndsWith("ches") || lower.EndsWith("shes"))
                variants.Add(word[..^2]);
            else
                variants.Add(word[..^2]);
        }
        else if (lower.EndsWith("s") && !lower.EndsWith("ss") && lower.Length > 2)
        {
            // "jerseys" → "jersey", "caps" → "cap"
            variants.Add(word[..^1]);
        }

        // Add plural → from singular
        if (!lower.EndsWith("s"))
        {
            if (lower.EndsWith("y") && lower.Length > 2 && !IsVowel(lower[^2]))
            {
                // "accessory" → "accessories"
                variants.Add(word[..^1] + "ies");
            }
            else if (lower.EndsWith("f") && lower.Length > 2)
            {
                // "scarf" → "scarves"
                variants.Add(word[..^1] + "ves");
            }
            else if (lower.EndsWith("ch") || lower.EndsWith("sh") || lower.EndsWith("x") || lower.EndsWith("z"))
            {
                variants.Add(word + "es");
            }
            else
            {
                variants.Add(word + "s");
            }
        }

        return variants.ToList();
    }

    private static bool IsVowel(char c) => "aeiou".Contains(c);

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
                    var terms = request.Q.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var primaryTerm = terms[0];
                    return query
                        .OrderByDescending(p => EF.Functions.Like(p.Name, primaryTerm))
                        .ThenByDescending(p => EF.Functions.Like(p.Name, $"{primaryTerm}%"))
                        .ThenByDescending(p => EF.Functions.Like(p.Name, $"%{primaryTerm}%"))
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
