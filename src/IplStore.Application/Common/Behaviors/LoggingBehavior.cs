using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IplStore.Application.Common.Behaviors;

/// <summary>Logs each request name and elapsed milliseconds. Warns on slow (>500ms) handlers.</summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await next();
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > 500)
                _logger.LogWarning("Handled {RequestName} in {Elapsed}ms (slow).", requestName, stopwatch.ElapsedMilliseconds);
            else
                _logger.LogInformation("Handled {RequestName} in {Elapsed}ms.", requestName, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "{RequestName} failed after {Elapsed}ms.", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
