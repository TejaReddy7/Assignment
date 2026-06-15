using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Middleware;

/// <summary>
/// Catches unhandled exceptions and returns RFC 7807 ProblemDetails instead of leaking stack traces.
/// Registered via app.UseExceptionHandler with IExceptionHandler (ASP.NET Core 8+ pattern).
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.TraceIdentifier;
        _logger.LogError(exception, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);

        var (status, title) = exception switch
        {
            OperationCanceledException => (499, "Request Cancelled"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = _env.IsDevelopment() ? exception.ToString() : "Please contact support with the correlation id.",
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["traceId"] = Activity.Current?.Id ?? correlationId
            }
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
