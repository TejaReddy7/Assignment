using IplStore.Domain.Primitives;
using MediatR;

namespace IplStore.Application.Common.Messaging;

/// <summary>
/// Wraps a domain event as a MediatR notification so it can be dispatched to
/// INotificationHandler&lt;DomainEventNotification&gt; subscribers after SaveChanges.
/// </summary>
public sealed record DomainEventNotification(IDomainEvent DomainEvent) : INotification;

/// <summary>
/// Convenience base so handlers can subscribe to a specific event type without unwrapping manually.
/// </summary>
public abstract class DomainEventHandler<TEvent> : INotificationHandler<DomainEventNotification>
    where TEvent : IDomainEvent
{
    public Task Handle(DomainEventNotification notification, CancellationToken cancellationToken)
        => notification.DomainEvent is TEvent typed
            ? HandleEvent(typed, cancellationToken)
            : Task.CompletedTask;

    protected abstract Task HandleEvent(TEvent domainEvent, CancellationToken cancellationToken);
}
