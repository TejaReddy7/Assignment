using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Reviews.GetReviews;

public sealed record GetReviewsQuery(Guid ProductId, int Page = 1, int PageSize = 10)
    : IRequest<Result<ProductReviewsDto>>;

public sealed class GetReviewsQueryHandler : IRequestHandler<GetReviewsQuery, Result<ProductReviewsDto>>
{
    private readonly IAppDbContext _db;

    public GetReviewsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<Result<ProductReviewsDto>> Handle(GetReviewsQuery request, CancellationToken cancellationToken)
    {
        var pagination = new PaginationParams { Page = request.Page, PageSize = request.PageSize };

        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == request.ProductId)
            .Select(p => new { p.Id, p.AverageRating, p.ReviewCount })
            .FirstOrDefaultAsync(cancellationToken);
        if (product is null) return DomainErrors.Product.NotFound;

        var query = _db.Reviews
            .AsNoTracking()
            .Where(r => r.ProductId == request.ProductId)
            .OrderByDescending(r => r.CreatedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var reviews = await query
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .Select(r => new ReviewDto(r.Id, r.ProductId, r.CustomerDisplayName,
                r.Rating, r.Title, r.Body, r.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new ProductReviewsDto(product.Id, product.AverageRating, product.ReviewCount,
            reviews, pagination.Page, pagination.PageSize, total);
    }
}
