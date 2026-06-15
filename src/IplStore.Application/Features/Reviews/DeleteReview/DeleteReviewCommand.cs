using IplStore.Application.Common;
using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Reviews.DeleteReview;

public sealed record DeleteReviewCommand(Guid ProductId, Guid ReviewId) : IRequest<Result>;

public sealed class DeleteReviewCommandHandler : IRequestHandler<DeleteReviewCommand, Result>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICacheService _cache;

    public DeleteReviewCommandHandler(IAppDbContext db, ICurrentUser currentUser, ICacheService cache)
    {
        _db = db;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result> Handle(DeleteReviewCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } customerId)
            return Result.Failure(DomainErrors.Auth.InvalidCredentials);

        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == request.ReviewId, cancellationToken);
        if (review is null) return Result.Failure(DomainErrors.Review.NotFound);

        // Only the author or an admin may delete a review.
        if (review.CustomerId != customerId && !_currentUser.IsAdmin)
            return Result.Failure(DomainErrors.Auth.Forbidden);

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == review.ProductId, cancellationToken);
        if (product is null) return Result.Failure(DomainErrors.Product.NotFound);

        _db.Reviews.Remove(review);
        await RatingRecalculator.RecomputeAsync(_db, product, pendingAddition: null, review.Id, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByPrefixAsync(CacheKeys.ProductPrefix, cancellationToken);
        return Result.Success();
    }
}
