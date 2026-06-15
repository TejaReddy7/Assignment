using IplStore.Application.Common;
using IplStore.Application.Common.Abstractions;
using IplStore.Application.Common.Behaviors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Catalog.GetProducts;

public sealed record GetProductsQuery(
    int Page = 1,
    int PageSize = 20,
    string? SortBy = "name",
    string? SortDir = "asc")
    : IRequest<Result<PagedResult<ProductListItemDto>>>, ICacheableQuery
{
    public string CacheKey => CacheKeys.ProductList(Page, PageSize, SortBy, SortDir);
    public TimeSpan? CacheDuration => TimeSpan.FromSeconds(60);
}

public sealed class GetProductsQueryHandler
    : IRequestHandler<GetProductsQuery, Result<PagedResult<ProductListItemDto>>>
{
    private readonly IAppDbContext _db;

    public GetProductsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<Result<PagedResult<ProductListItemDto>>> Handle(
        GetProductsQuery request, CancellationToken cancellationToken)
    {
        var pagination = new PaginationParams { Page = request.Page, PageSize = request.PageSize };

        var query = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Include(p => p.Franchise)
            .Include(p => p.Variants)
            .AsQueryable();

        query = ApplySort(query, request.SortBy, request.SortDir);

        var total = await query.CountAsync(cancellationToken);

        var products = await query
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var items = products.Select(p => p.ToListItemDto()).ToList();

        return new PagedResult<ProductListItemDto>(items, pagination.Page, pagination.PageSize, total);
    }

    private static IQueryable<Domain.Entities.Product> ApplySort(
        IQueryable<Domain.Entities.Product> query, string? sortBy, string? sortDir)
    {
        var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        return (sortBy?.ToLowerInvariant()) switch
        {
            "price" => descending
                ? query.OrderByDescending(p => p.BasePrice.Amount)
                : query.OrderBy(p => p.BasePrice.Amount),
            "rating" => descending
                ? query.OrderByDescending(p => p.AverageRating)
                : query.OrderBy(p => p.AverageRating),
            "newest" => query.OrderByDescending(p => p.CreatedAtUtc),
            _ => descending
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name)
        };
    }
}
