using IplStore.Application.Common;
using IplStore.Application.Common.Abstractions;
using IplStore.Application.Common.Behaviors;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Catalog.GetProductBySlug;

public sealed record GetProductBySlugQuery(string Slug)
    : IRequest<Result<ProductDetailsDto>>, ICacheableQuery
{
    public string CacheKey => CacheKeys.ProductDetails(Slug);
    public TimeSpan? CacheDuration => TimeSpan.FromSeconds(60);
}

public sealed class GetProductBySlugQueryHandler
    : IRequestHandler<GetProductBySlugQuery, Result<ProductDetailsDto>>
{
    private readonly IAppDbContext _db;

    public GetProductBySlugQueryHandler(IAppDbContext db) => _db = db;

    public async Task<Result<ProductDetailsDto>> Handle(
        GetProductBySlugQuery request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Franchise)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Slug == new Domain.ValueObjects.Slug(request.Slug) && p.IsActive,
                cancellationToken);

        if (product is null) return DomainErrors.Product.NotFound;

        return product.ToDetailsDto();
    }
}
