using IplStore.Api;
using IplStore.Api.Middleware;
using IplStore.Application;
using IplStore.Infrastructure;
using IplStore.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging from the first line.
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/log-.txt", rollingInterval: Serilog.RollingInterval.Day, retainedFileCountLimit: 7));

// Composition root: each layer registers its own services.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApiServices(builder.Configuration);

var app = builder.Build();

// Initialize + seed the database on startup (idempotent).
await DatabaseInitializer.InitializeAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
        options.WithTitle("IPL Franchise Store API")
            .WithTheme(ScalarTheme.Mars));
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseHttpsRedirection();
app.UseCors(IplStore.Api.DependencyInjection.CorsPolicy);

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

// Exposed for WebApplicationFactory in integration tests.
public partial class Program;
