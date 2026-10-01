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
/// just that the code compiles. Not RunDueReportsAsync's scan query: `NextRunAt <= now` on a
/// DateTimeOffset column can't be translated by the SQLite test provider (the same limitation
/// GetScheduledReportsQuery's own ORDER BY already has to work around) - a pre-existing test
/// infrastructure gap, not something to change in production code for test convenience.</summary>
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

    public void Dispose() => _db.Dispose();
}
