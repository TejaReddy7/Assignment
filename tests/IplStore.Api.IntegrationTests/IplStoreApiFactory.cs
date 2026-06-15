using IplStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IplStore.Api.IntegrationTests;

/// <summary>
/// Boots the real API in-process against a private in-memory SQLite database.
/// A single open connection is shared so the schema + seed data survive for the
/// lifetime of the factory. The app's own startup seeding populates the catalog.
/// </summary>
public sealed class IplStoreApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Replace the file-based AppDbContext with our shared in-memory connection.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connection);
                options.ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId
                        .PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
            });
        });
    }

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }
}

internal static class ServiceCollectionTestExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var d in descriptors) services.Remove(d);
    }
}
