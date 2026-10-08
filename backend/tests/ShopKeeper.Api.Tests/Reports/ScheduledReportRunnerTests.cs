namespace ShopKeeper.Api.Tests.Reports;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Infrastructure.BackgroundJobs;
using ShopKeeper.Infrastructure.Identity;
using ReportExportFormat = ShopKeeper.Domain.Entities.ReportExportFormat;

/// <summary>Exercises RunOneAsync - made internal specifically so tests can call it directly
/// (see the Infrastructure csproj's InternalsVisibleTo) - through a real IServiceScopeFactory,
/// rather than calling the try/catch logic by inspection, since the whole point of this suite is
/// proving the LastRunSucceeded/LastRunError bookkeeping actually reflects what happened, not
/// just that the code compiles. RunDueReportsAsync's own scan query is covered separately below -
/// it used to be skipped here on the theory that `NextRunAt <= now` on a DateTimeOffset column
/// not translating on SQLite (the same limitation GetScheduledReportsQuery's own ORDER BY already
/// works around) was a test-only quirk, since the SaaS build only ever runs on Postgres. That
/// stopped being true once the offline edition made SQLite a real production provider - see
/// ScheduledReportRunner.RunDueReportsAsync's doc comment for the production-code fix.</summary>
public class ScheduledReportRunnerTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    private class SucceedingEmailSender : IEmailSender
    {
        public int ReportEmailsSent { get; private set; }

        public Task SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task SendPasswordResetAsync(string toEmail, string firstName, string resetToken, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task SendBusinessInviteAsync(string toEmail, string businessName, string inviterName, string inviteToken, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task SendReportEmailAsync(
            string toEmail, string businessName, byte[] attachment, string attachmentFileName, string contentType, CancellationToken ct = default)
        {
            ReportEmailsSent++;
            return Task.CompletedTask;
        }
    }

    private class ThrowingEmailSender : IEmailSender
    {
        public Task SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task SendPasswordResetAsync(string toEmail, string firstName, string resetToken, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task SendBusinessInviteAsync(string toEmail, string businessName, string inviterName, string inviteToken, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task SendReportEmailAsync(
            string toEmail, string businessName, byte[] attachment, string attachmentFileName, string contentType, CancellationToken ct = default) =>
            throw new InvalidOperationException("Simulated delivery failure (e.g. recipient rejected).");
    }

    private ScheduledReportRunner BuildRunner(IAppDbContext db, TestCurrentUserService owner, IEmailSender emailSender)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<MediatR.ISender>(new TestSender(db, owner));
        services.AddSingleton(emailSender);
        var provider = services.BuildServiceProvider();
        return new ScheduledReportRunner(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ScheduledReportRunner>.Instance);
    }

    private async Task<ScheduledReport> SeedDueScheduleAsync(IAppDbContext db, PosTestFixture.SeededBusiness seeded)
    {
        var report = new ScheduledReport
        {
            BusinessId = seeded.BusinessId,
            Frequency = ScheduledReportFrequency.Daily,
            Format = ReportExportFormat.Pdf,
            RecipientEmails = "owner@shop.test",
            CreatedByUserId = seeded.OwnerId,
            IsActive = true,
            NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1), // already due
        };
        db.ScheduledReports.Add(report);
        await db.SaveChangesAsync(CancellationToken.None);
        return report;
    }

    [Fact]
    public async Task RunDueReportsAsync_SuccessfulRun_RecordsSuccessAndAdvancesNextRunAt()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var scheduled = await SeedDueScheduleAsync(context, seeded);
        var emailSender = new SucceedingEmailSender();
        var runner = BuildRunner(context, owner, emailSender);
        var originalNextRunAt = scheduled.NextRunAt;

        await runner.RunOneAsync(scheduled.Id, CancellationToken.None);

        var reloaded = await context.ScheduledReports.FindAsync([scheduled.Id], CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.True(reloaded!.LastRunSucceeded);
        Assert.Null(reloaded.LastRunError);
        Assert.NotNull(reloaded.LastRunAt);
        Assert.True(reloaded.NextRunAt > originalNextRunAt); // advanced to the next occurrence
        Assert.Equal(1, emailSender.ReportEmailsSent);
    }

    [Fact]
    public async Task RunDueReportsAsync_DeliveryFails_RecordsFailureButStillAdvancesNextRunAt()
    {
        // The core regression test for the dead-letter-visibility gap: before this fix,
        // LastRunAt advanced identically whether the run succeeded or failed, so a
        // permanently-broken schedule was indistinguishable from a healthy one anywhere a
        // business owner could see. NextRunAt must still advance (a broken schedule shouldn't
        // retry every tick forever), but the failure itself must now be visible.
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var scheduled = await SeedDueScheduleAsync(context, seeded);
        var runner = BuildRunner(context, owner, new ThrowingEmailSender());
        var originalNextRunAt = scheduled.NextRunAt;

        await runner.RunOneAsync(scheduled.Id, CancellationToken.None);

        var reloaded = await context.ScheduledReports.FindAsync([scheduled.Id], CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.False(reloaded!.LastRunSucceeded);
        Assert.Contains("Simulated delivery failure", reloaded.LastRunError);
        Assert.NotNull(reloaded.LastRunAt);
        Assert.True(reloaded.NextRunAt > originalNextRunAt); // still advances - a broken schedule must not retry every tick forever
    }

    [Fact]
    public async Task RunDueReportsAsync_AgainstRealSqlite_FindsOnlyTheDueScheduleAndRunsIt()
    {
        // Regression test: EF Core's SQLite provider can't translate `NextRunAt <= now` (a
        // relational comparison on a DateTimeOffset column) and throws InvalidOperationException
        // on every tick - this runs the real scan query (not just RunOneAsync, which every other
        // test in this file calls directly) against the real SQLite test database to prove it
        // no longer throws and still picks the right rows.
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var due = await SeedDueScheduleAsync(context, seeded);

        var notDue = new ScheduledReport
        {
            BusinessId = seeded.BusinessId,
            Frequency = ScheduledReportFrequency.Daily,
            Format = ReportExportFormat.Pdf,
            RecipientEmails = "owner@shop.test",
            CreatedByUserId = seeded.OwnerId,
            IsActive = true,
            NextRunAt = DateTimeOffset.UtcNow.AddDays(1), // not due yet
        };
        var inactiveButOverdue = new ScheduledReport
        {
            BusinessId = seeded.BusinessId,
            Frequency = ScheduledReportFrequency.Daily,
            Format = ReportExportFormat.Pdf,
            RecipientEmails = "owner@shop.test",
            CreatedByUserId = seeded.OwnerId,
            IsActive = false, // disabled - must never run even though it's overdue
            NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        context.ScheduledReports.AddRange(notDue, inactiveButOverdue);
        await context.SaveChangesAsync(CancellationToken.None);

        var emailSender = new SucceedingEmailSender();
        var runner = BuildRunner(context, owner, emailSender);

        await runner.RunDueReportsAsync(CancellationToken.None);

        Assert.Equal(1, emailSender.ReportEmailsSent); // only the one genuinely-due schedule ran
        var reloadedDue = await context.ScheduledReports.FindAsync([due.Id], CancellationToken.None);
        Assert.True(reloadedDue!.LastRunSucceeded);
    }

    public void Dispose() => _db.Dispose();
}
