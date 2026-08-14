namespace ShopKeeper.Api.Tests.TestHelpers;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Infrastructure.Persistence;

/// <summary>
/// Unlike SqliteTestDatabase (one shared SqliteConnection - ADO.NET connections can't run
/// two commands at once, so it can't prove genuine concurrency), this backs each
/// CreateContext() call with its own independent connection onto the same named,
/// shared-cache in-memory SQLite database, so real overlapping Task.WhenAll commands
/// actually race at the SQLite engine level instead of implicitly serializing through one
/// connection object. A busy timeout is set so a genuine write/write collision blocks
/// briefly (representative of real concurrent behavior) instead of immediately throwing
/// SQLITE_BUSY. One "anchor" connection is kept open for the object's lifetime - a
/// shared-cache in-memory database is destroyed the instant its last connection closes.
/// </summary>
public sealed class ConcurrentSqliteTestDatabase : ITestDatabase, IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _anchor;
    private readonly List<SqliteConnection> _connections = [];
    private readonly object _gate = new();
    private bool _created;

    public ConcurrentSqliteTestDatabase()
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"file:{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 5,
        }.ToString();

        _anchor = new SqliteConnection(_connectionString);
        _anchor.Open();
    }

    public AppDbContext CreateContext(ICurrentUserService currentUser)
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var context = new AppDbContext(options, currentUser);

        lock (_gate)
        {
            _connections.Add(connection);
            if (!_created)
            {
                context.Database.EnsureCreated();
                _created = true;
            }
        }

        return context;
    }

    public void Dispose()
    {
        foreach (var connection in _connections)
        {
            connection.Dispose();
        }

        _anchor.Dispose();
    }
}
