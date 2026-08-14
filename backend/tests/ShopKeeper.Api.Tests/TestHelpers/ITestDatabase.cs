namespace ShopKeeper.Api.Tests.TestHelpers;

using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Infrastructure.Persistence;

/// <summary>Common surface both SqliteTestDatabase (single shared connection - fast, used by
/// almost every test) and ConcurrentSqliteTestDatabase (independent connections onto one
/// shared-cache database - needed for genuine Task.WhenAll concurrency tests) expose, so
/// PosTestFixture.SeedAsync works against either without duplicating seed logic.</summary>
public interface ITestDatabase
{
    AppDbContext CreateContext(ICurrentUserService currentUser);
}
