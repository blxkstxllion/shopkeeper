namespace ShopKeeper.Api.Tests.Reports;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Reports.Commands;
using ShopKeeper.Application.Reports.Queries;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;
using ReportExportFormat = ShopKeeper.Domain.Entities.ReportExportFormat;

// Scheduled reports require the Reports plan feature (CreateScheduledReportCommand/
// GetScheduledReportsQuery both implement IRequirePlanFeature) - every test here mirrors
// RequirePlanTierBehaviorTests' SetTierAsync step, since PosTestFixture seeds a Free-tier
// business by default and Free has no report access at all.
public class ScheduledReportCommandsTests : IDisposable
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
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private async Task SetReportsTierAsync(PosTestFixture.SeededBusiness seeded, TestCurrentUserService owner)
    {
        var context = _db.CreateContext(owner);
        var business = await context.Businesses.SingleAsync(b => b.Id == seeded.BusinessId);
        business.PlanTier = PlanTier.Business;
        await context.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Create_ThenGet_ReturnsIt()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        await SetReportsTierAsync(seeded, owner);
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        var created = await sender.Send(
            new CreateScheduledReportCommand(
                null, ScheduledReportFrequency.Weekly, ReportExportFormat.Pdf, ["owner@shop.test"]),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(ScheduledReportFrequency.Weekly, created.Frequency);
        Assert.True(created.IsActive);
        Assert.True(created.NextRunAt > DateTimeOffset.UtcNow);

        var all = await sender.Send(new GetScheduledReportsQuery(), CancellationToken.None);
        var found = Assert.Single(all);
        Assert.Equal(created.Id, found.Id);
        Assert.Equal(["owner@shop.test"], found.RecipientEmails);
    }

    [Fact]
    public async Task Delete_RemovesIt()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        await SetReportsTierAsync(seeded, owner);
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        var created = await sender.Send(
            new CreateScheduledReportCommand(
                null, ScheduledReportFrequency.Daily, ReportExportFormat.Word, ["owner@shop.test"]),
            CancellationToken.None);

        await sender.Send(new DeleteScheduledReportCommand(created.Id), CancellationToken.None);

        var all = await sender.Send(new GetScheduledReportsQuery(), CancellationToken.None);
        Assert.Empty(all);
    }

    [Fact]
    public async Task DifferentBusiness_CannotSeeAnotherBusinesssSchedule()
    {
        var seededA = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "owner-a@shop.test");
        var ownerA = seededA.AsOwner();
        await SetReportsTierAsync(seededA, ownerA);
        await sender_Create(ownerA);

        var seededB = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "owner-b@shop.test");
        var ownerB = seededB.AsOwner();
        await SetReportsTierAsync(seededB, ownerB);
        var contextB = _db.CreateContext(ownerB);
        var senderB = BuildSender(contextB, ownerB);

        var visibleToB = await senderB.Send(new GetScheduledReportsQuery(), CancellationToken.None);
        Assert.Empty(visibleToB); // tenant isolation - see AppDbContext's global query filter

        async Task sender_Create(TestCurrentUserService owner)
        {
            var context = _db.CreateContext(owner);
            var sender = BuildSender(context, owner);
            await sender.Send(
                new CreateScheduledReportCommand(
                    null, ScheduledReportFrequency.Daily, ReportExportFormat.Pdf, ["a@shop.test"]),
                CancellationToken.None);
        }
    }

    public void Dispose() => _db.Dispose();
}
