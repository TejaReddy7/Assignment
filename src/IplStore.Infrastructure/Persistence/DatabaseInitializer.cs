using IplStore.Infrastructure.Identity;
using IplStore.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IplStore.Infrastructure.Persistence;

/// <summary>
/// One-call startup routine: applies migrations (or ensures schema) and seeds reference data.
/// Safe to run on every boot — all seeders are idempotent.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        var db = sp.GetRequiredService<AppDbContext>();

        try
        {
            if (db.Database.IsRelational())
                await db.Database.MigrateAsync(ct);
            else
                await db.Database.EnsureCreatedAsync(ct);

            var roleManager = sp.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

            await IdentitySeeder.SeedAsync(roleManager, userManager);
            await CatalogSeeder.SeedAsync(db, ct);
            await CouponSeeder.SeedAsync(db, ct);

            logger.LogInformation("Database initialized and seeded successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed.");
            throw;
        }
    }
}
