namespace IplStore.Domain.Primitives;

public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    protected Entity(TId id) => Id = id;

    // EF Core constructor
    protected Entity() => Id = default!;

    public TId Id { get; protected set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public bool Equals(Entity<TId>? other)
        => other is not null && other.GetType() == GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override bool Equals(object? obj) => obj is Entity<TId> e && Equals(e);

    public override int GetHashCode() => Id.GetHashCode();
}

/// <summary>
/// Marker interface for aggregate roots. Only aggregate roots have repositories
/// and are loaded/persisted as units.
/// </summary>
public interface IAggregateRoot;

public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTime? DeletedAtUtc { get; }
    void SoftDelete();
}

public interface IAuditable
{
    DateTime CreatedAtUtc { get; }
    DateTime? UpdatedAtUtc { get; }
}
