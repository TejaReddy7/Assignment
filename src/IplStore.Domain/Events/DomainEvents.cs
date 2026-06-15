using IplStore.Domain.Primitives;
using IplStore.Domain.ValueObjects;

namespace IplStore.Domain.Events;

public sealed record OrderPlacedEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Money Total) : DomainEvent;

public sealed record OrderCancelledEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    IReadOnlyList<(Guid VariantId, int Quantity)> ItemsToRestock) : DomainEvent;

public sealed record ReviewSubmittedEvent(
    Guid ProductId,
    Guid ReviewId,
    int Rating) : DomainEvent;

public sealed record ProductRatingUpdatedEvent(
    Guid ProductId,
    decimal NewAverage,
    int NewReviewCount) : DomainEvent;
