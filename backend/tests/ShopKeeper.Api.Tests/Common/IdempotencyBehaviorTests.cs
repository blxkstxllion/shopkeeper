namespace ShopKeeper.Api.Tests.Common;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application;
using ShopKeeper.Application.Advisor;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Expenses.Commands;
using ShopKeeper.Infrastructure.Ai;
using ShopKeeper.Infrastructure.Identity;

/// <summary>
/// IdempotencyBehavior runs as a MediatR pipeline behavior, not code any handler calls
/// directly - like RequirePlanTierBehaviorTests, these go through a real ISender rather than
/// constructing handlers by hand, which is the only way this behavior actually runs at all.
/// Exercised via CreateExpenseCategoryCommand as a representative offline-eligible command -
/// the behavior itself is generic, so one representative command is enough to prove the
/// mechanism; each command's own opt-in (ISupportsClientRequestId) is a one-line addition
/// with nothing command-specific left to test.
/// </summary>
public class IdempotencyBehaviorTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    private ISender BuildSender(IAppDbContext context, ICurrentUserService currentUser)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton(context);
        services.AddSingleton(currentUser);
        services.AddSingleton<IAdvisorNarrator>(new PassthroughAdvisorNarrator());
        services.AddSingleton<IAdvisorConversationClient>(new UnavailableAdvisorConversationClient());
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task WithoutClientRequestId_EachCallCreatesANewRecord()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        var first = await sender.Send(new CreateExpenseCategoryCommand("Utilities", null), CancellationToken.None);
        var second = await sender.Send(new CreateExpenseCategoryCommand("Utilities", null), CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task WithSameClientRequestId_SecondCallReturnsTheFirstResultInsteadOfDuplicating()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);
        var clientRequestId = Guid.NewGuid();

        var first = await sender.Send(new CreateExpenseCategoryCommand("Rent", null, clientRequestId), CancellationToken.None);
        var replay = await sender.Send(new CreateExpenseCategoryCommand("Rent", null, clientRequestId), CancellationToken.None);

        Assert.Equal(first.Id, replay.Id);

        var freshContext = _db.CreateContext(owner);
        var categoryCount = await freshContext.ExpenseCategories.CountAsync(c => c.Name == "Rent");
        Assert.Equal(1, categoryCount);
    }

    [Fact]
    public async Task WithDifferentClientRequestIds_BothCallsCreateSeparateRecords()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        var first = await sender.Send(new CreateExpenseCategoryCommand("Supplies", null, Guid.NewGuid()), CancellationToken.None);
        var second = await sender.Send(new CreateExpenseCategoryCommand("Supplies", null, Guid.NewGuid()), CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task DifferentBusinesses_ReusingTheSameClientRequestId_DoNotCollide()
    {
        var seededA = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "owner-a@shop.test");
        var ownerA = seededA.AsOwner();
        var senderA = BuildSender(_db.CreateContext(ownerA), ownerA);

        var seededB = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "owner-b@shop.test");
        var ownerB = seededB.AsOwner();
        var senderB = BuildSender(_db.CreateContext(ownerB), ownerB);

        var sharedClientRequestId = Guid.NewGuid();

        var resultA = await senderA.Send(new CreateExpenseCategoryCommand("Marketing", null, sharedClientRequestId), CancellationToken.None);
        var resultB = await senderB.Send(new CreateExpenseCategoryCommand("Marketing", null, sharedClientRequestId), CancellationToken.None);

        Assert.NotEqual(resultA.Id, resultB.Id);
    }

    public void Dispose() => _db.Dispose();
}
