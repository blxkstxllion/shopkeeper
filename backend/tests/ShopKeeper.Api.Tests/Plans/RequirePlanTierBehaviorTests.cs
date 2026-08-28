namespace ShopKeeper.Api.Tests.Plans;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application;
using ShopKeeper.Application.Advisor;
using ShopKeeper.Application.Advisor.Commands;
using ShopKeeper.Application.Advisor.Queries;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Reports.Queries;
using ShopKeeper.Application.Roles.Commands;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Ai;
using ShopKeeper.Infrastructure.Identity;

/// <summary>
/// RequirePlanTierBehavior runs as a MediatR pipeline behavior, not code any handler calls
/// directly - like AuditLoggingBehaviorTests, these go through a real ISender rather than
/// constructing handlers by hand, which is the only way this behavior actually runs at all.
/// </summary>
public class RequirePlanTierBehaviorTests : IDisposable
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

    private async Task SetTierAsync(SqliteTestDatabase db, PosTestFixture.SeededBusiness seeded, TestCurrentUserService owner, PlanTier tier)
    {
        var context = db.CreateContext(owner);
        var business = await context.Businesses.SingleAsync(b => b.Id == seeded.BusinessId);
        business.PlanTier = tier;
        await context.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Reports_OnFreeTier_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            sender.Send(new GetProfitabilityReportQuery(today, today, null), CancellationToken.None));
        Assert.Contains("Reports", ex.Message);
    }

    [Fact]
    public async Task Reports_OnBusinessTier_Succeeds()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        await SetTierAsync(_db, seeded, owner, PlanTier.Business);
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var report = await sender.Send(new GetProfitabilityReportQuery(today, today, null), CancellationToken.None);
        Assert.NotNull(report);
    }

    [Fact]
    public async Task Advisor_OnBusinessTierWithoutAi_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        await SetTierAsync(_db, seeded, owner, PlanTier.Business); // Reports yes, AI no
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            sender.Send(new GetAdvisorAnswerQuery(AdvisorQuestionId.AmIProfitable, null), CancellationToken.None));
        Assert.Contains("Advisor", ex.Message);
    }

    [Fact]
    public async Task Advisor_OnBusinessAiTier_Succeeds()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        await SetTierAsync(_db, seeded, owner, PlanTier.BusinessAi);
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        var answer = await sender.Send(new GetAdvisorAnswerQuery(AdvisorQuestionId.AmIProfitable, null), CancellationToken.None);
        Assert.NotNull(answer.Answer);
    }

    [Fact]
    public async Task AskAdvisor_OnBusinessTierWithoutAi_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        await SetTierAsync(_db, seeded, owner, PlanTier.Business); // Reports yes, AI no
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            sender.Send(new AskAdvisorCommand("How's my revenue?", null), CancellationToken.None));
        Assert.Contains("Advisor", ex.Message);
    }

    [Fact]
    public async Task AskAdvisor_OnBusinessAiTier_Succeeds()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        await SetTierAsync(_db, seeded, owner, PlanTier.BusinessAi);
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        var answer = await sender.Send(new AskAdvisorCommand("How's my revenue?", null), CancellationToken.None);
        Assert.NotNull(answer.Answer); // UnavailableAdvisorConversationClient -> caught -> fallback text, not a thrown exception.
    }

    [Fact]
    public async Task AdvisorQuestions_OnFreeTier_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            sender.Send(new GetAdvisorQuestionsQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task CreateRole_OnBusinessTier_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        await SetTierAsync(_db, seeded, owner, PlanTier.Business);
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            sender.Send(new CreateRoleCommand("Trainee", null, []), CancellationToken.None));
        Assert.Contains("Custom roles", ex.Message);
    }

    [Fact]
    public async Task CreateRole_OnEnterpriseTier_Succeeds()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        await SetTierAsync(_db, seeded, owner, PlanTier.Enterprise);
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        var roleId = await sender.Send(new CreateRoleCommand("Trainee", null, []), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, roleId);
    }

    public void Dispose() => _db.Dispose();
}
