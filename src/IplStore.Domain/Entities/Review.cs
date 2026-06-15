using IplStore.Domain.Errors;
using IplStore.Domain.Events;
using IplStore.Domain.Primitives;
using IplStore.Shared;

namespace IplStore.Domain.Entities;

public sealed class Review : Entity<Guid>, IAggregateRoot
{
    private Review() { } // EF

    private Review(Guid id, Guid productId, Guid customerId, string customerDisplayName,
        int rating, string title, string body)
        : base(id)
    {
        ProductId = productId;
        CustomerId = customerId;
        CustomerDisplayName = customerDisplayName;
        Rating = rating;
        Title = title;
        Body = body;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid ProductId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string CustomerDisplayName { get; private set; } = default!;
    public int Rating { get; private set; }
    public string Title { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }

    public static Result<Review> Create(Guid productId, Guid customerId, string customerDisplayName,
        int rating, string title, string body)
    {
        if (rating is < 1 or > 5) return DomainErrors.Review.RatingOutOfRange;
        if (string.IsNullOrWhiteSpace(title)) return Error.Validation("review.title_required", "Title is required.");
        if (title.Length > 120) return Error.Validation("review.title_too_long", "Title cannot exceed 120 characters.");
        if (string.IsNullOrWhiteSpace(body)) return Error.Validation("review.body_required", "Body is required.");
        if (body.Length > 2000) return Error.Validation("review.body_too_long", "Body cannot exceed 2000 characters.");

        var review = new Review(Guid.NewGuid(), productId, customerId, customerDisplayName,
            rating, title.Trim(), body.Trim());
        review.RaiseDomainEvent(new ReviewSubmittedEvent(productId, review.Id, rating));
        return review;
    }
}
