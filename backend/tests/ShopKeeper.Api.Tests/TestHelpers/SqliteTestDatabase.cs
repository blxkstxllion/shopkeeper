namespace ShopKeeper.Api.Tests.TestHelpers;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Infrastructure.Persistence;

/// <summary>
/// Backs tests with a real (SQLite, in-memory) relational database instead of Docker
/// Postgres - fast, no external dependency, and still exercises EF Core's actual query
/// translation, unique indexes, and HasData seeding (unlike the InMemory provider).
/// The connection must stay open for the database's lifetime; closing it drops everything.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _created;

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public AppDbContext CreateContext(ShopKeeper.Application.Common.Interfaces.ICurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        var context = new AppDbContext(options, currentUser);

        if (!_created)
        {
            context.Database.EnsureCreated();
            _created = true;
        }

        return context;
    }

    public void Dispose() => _connection.Dispose();
}
