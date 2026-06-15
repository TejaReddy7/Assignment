using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IplStore.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations` work without booting the full API host.
/// Uses SQLite so the design-time tooling never needs a live SQL Server.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=iplstore.db")
            .Options;

        return new AppDbContext(options);
    }
}
