using System.Collections.Concurrent;
using IplStore.Application.Common.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace IplStore.Infrastructure.Services;

/// <summary>
/// In-memory cache implementation. Tracks keys so we can support RemoveByPrefix
/// (IMemoryCache has no native enumeration). Swap this for a Redis-backed
/// IDistributedCache implementation in production with zero changes to callers.
/// </summary>
public sealed class MemoryCacheService : ICacheService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(60);
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, byte> _keys = new();

    public MemoryCacheService(IMemoryCache cache) => _cache = cache;

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        => Task.FromResult(_cache.TryGetValue(key, out var value) ? (T?)value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var options = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl ?? DefaultTtl };
        options.RegisterPostEvictionCallback((evictedKey, _, _, _) => _keys.TryRemove((string)evictedKey, out _));
        _cache.Set(key, value, options);
        _keys.TryAdd(key, 0);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        foreach (var key in _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
        }
        return Task.CompletedTask;
    }
}
