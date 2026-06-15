using FluentValidation;
using IplStore.Application.Common;
using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Entities;
using IplStore.Domain.Enums;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Reviews.AddReview;

public sealed record AddReviewCommand(Guid ProductId, int Rating, string Title, string Body)
    : IRequest<Result<ReviewDto>>;

public sealed class AddReviewCommandValidator : AbstractValidator<AddReviewCommand>
{
    public AddReviewCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
    }
}

public sealed class AddReviewCommandHandler : IRequestHandler<AddReviewCommand, Result<ReviewDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityService _identity;
    private readonly ICacheService _cache;

    public AddReviewCommandHandler(
        IAppDbContext db, ICurrentUser currentUser, IIdentityService identity, ICacheService cache)
    {
        _db = db;
        _currentUser = currentUser;
        _identity = identity;
        _cache = cache;
    }

    public async Task<Result<ReviewDto>> Handle(AddReviewCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } customerId)
            return DomainErrors.Auth.InvalidCredentials;

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (product is null) return DomainErrors.Product.NotFound;

        // One review per (product, customer).
        var alreadyReviewed = await _db.Reviews
            .AnyAsync(r => r.ProductId == request.ProductId && r.CustomerId == customerId, cancellationToken);
        if (alreadyReviewed) return DomainErrors.Review.AlreadyReviewed;

        // Verified buyer: the customer must have a non-cancelled order containing this product.
        var hasPurchased = await _db.Orders
            .Where(o => o.CustomerId == customerId && o.Status != OrderStatus.Cancelled)
            .SelectMany(o => o.Items)
            .AnyAsync(i => i.ProductId == request.ProductId, cancellationToken);
        if (!hasPurchased) return DomainErrors.Review.NotPurchased;

        var profile = await _identity.FindByIdAsync(customerId, cancellationToken);
        var displayName = profile.IsSuccess ? profile.Value.FullName : "Verified Buyer";

        var reviewResult = Review.Create(request.ProductId, customerId, displayName,
            request.Rating, request.Title, request.Body);
        if (reviewResult.IsFailure) return reviewResult.Error;

        var review = reviewResult.Value;
        _db.Reviews.Add(review);

        // Recompute the denormalized rating to include this new (not-yet-saved) review.
        await RatingRecalculator.RecomputeAsync(_db, product, review, pendingRemovalId: null, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByPrefixAsync(CacheKeys.ProductPrefix, cancellationToken);

        return new ReviewDto(review.Id, review.ProductId, review.CustomerDisplayName,
            review.Rating, review.Title, review.Body, review.CreatedAtUtc);
    }
}
