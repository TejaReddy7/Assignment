using System.Threading.RateLimiting;
using Asp.Versioning;
using IplStore.Api.Common;
using IplStore.Api.Middleware;
using IplStore.Api.OpenApi;
using IplStore.Application.Common.Abstractions;
using Microsoft.AspNetCore.RateLimiting;

namespace IplStore.Api;

public static class DependencyInjection
{
    public const string CorsPolicy = "IplStoreClient";

    public static IServiceCollection AddApiServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        AddApiVersioning(services);
        AddSwagger(services);
        AddCors(services, configuration);
        AddRateLimiting(services);

        services.AddHealthChecks();

        return services;
    }

    private static void AddApiVersioning(IServiceCollection services)
    {
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });
    }

    private static void AddSwagger(IServiceCollection services)
    {
        // .NET 10 native OpenAPI document generation (no Swashbuckle).
        // Scalar renders the interactive UI at /scalar/v1.
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new Microsoft.OpenApi.OpenApiInfo
                {
                    Title = "IPL Franchise Store API",
                    Version = "v1",
                    Description = "Ecommerce backend for IPL franchise merchandise. Clean Architecture + CQRS."
                };
                return Task.CompletedTask;
            });
            options.AddDocumentTransformer<JwtSecuritySchemeTransformer>();
        });
    }

    private static void AddCors(IServiceCollection services, IConfiguration configuration)
    {
        var originsList = new List<string>();

        // 1. Array configuration (e.g. from appsettings.json)
        var originsArray = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (originsArray != null)
        {
            originsList.AddRange(originsArray);
        }

        // 2. Comma-separated string from environment variables
        var originsString = configuration["Cors:AllowedOrigins"];
        if (!string.IsNullOrWhiteSpace(originsString))
        {
            originsList.AddRange(originsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        if (originsList.Count == 0)
        {
            originsList.Add("http://localhost:5173");
        }

        services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
            policy.WithOrigins(originsList.Distinct().ToArray())
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders("X-Correlation-Id")));
    }

    private static void AddRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Per-IP fixed window: generous for reads, applied globally.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // Stricter named policy for write/auth endpoints.
            options.AddFixedWindowLimiter("write", limiter =>
            {
                limiter.PermitLimit = 20;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
            });
        });
    }
}
