using IplStore.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Tests.Common;

/// <summary>
/// Spins up a real AppDbContext backed by a private in-memory SQLite database.
/// SQLite (unlike the EF in-memory provider) supports transactions, which our
/// order-placement handler relies on. Each harness owns one open connection so the
/// schema persists for the lifetime of the test.
/// </summary>
public sealed class SqliteTestHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId
                    .PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
            .Options;

        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public AppDbContext Context { get; }

    public AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId
                    .PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
            .Options;
        return new AppDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
