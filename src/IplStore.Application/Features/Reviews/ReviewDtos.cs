namespace IplStore.Application.Features.Reviews;

public sealed record ReviewDto(
    Guid Id,
    Guid ProductId,
    string CustomerDisplayName,
    int Rating,
    string Title,
    string Body,
    DateTime CreatedAtUtc);

public sealed record ProductReviewsDto(
    Guid ProductId,
    decimal AverageRating,
    int ReviewCount,
    IReadOnlyList<ReviewDto> Reviews,
    int Page,
    int PageSize,
    int TotalCount);
