namespace IplStore.Application.Common.Behaviors;

/// <summary>
/// Marks a query whose response can be cached. CacheKey must be unique per result set.
/// Implemented by read queries (e.g. product list/details) to opt into CachingBehavior.
/// </summary>
public interface ICacheableQuery
{
    string CacheKey { get; }
    TimeSpan? CacheDuration => null; // null => provider default
}
