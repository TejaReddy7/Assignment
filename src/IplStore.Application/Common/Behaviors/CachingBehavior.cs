using IplStore.Application.Common.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IplStore.Application.Common.Behaviors;

/// <summary>
/// Transparently caches responses for requests implementing ICacheableQuery.
/// Cache-hit short-circuits the handler; cache-miss runs it and stores the result.
/// Writes invalidate via ICacheService.RemoveByPrefix in the relevant handlers.
/// </summary>
public sealed class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICacheService _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(ICacheService cache, ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICacheableQuery cacheable)
            return await next();

        var cached = await _cache.GetAsync<TResponse>(cacheable.CacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Cache HIT for {CacheKey}.", cacheable.CacheKey);
            return cached;
        }

        var response = await next();
        await _cache.SetAsync(cacheable.CacheKey, response, cacheable.CacheDuration, cancellationToken);
        _logger.LogDebug("Cache MISS for {CacheKey} — stored.", cacheable.CacheKey);
        return response;
    }
}
