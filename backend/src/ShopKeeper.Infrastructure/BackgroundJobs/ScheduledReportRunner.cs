namespace ShopKeeper.Infrastructure.BackgroundJobs;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Reports;
using ShopKeeper.Application.Reports.Commands;
using ShopKeeper.Domain.Entities;

/// <summary>
/// Polls for due ScheduledReport rows and emails each one out - this app's first background
/// job, so there's no existing scheduler to hook into (Notifications is frontend-polling, not
/// a server-side job). A simple timer loop rather than a job-queue library (Hangfire etc.):
/// this is low-volume (a handful of schedules per business, checked hourly), and the existing
/// "polling over infra" precedent (Notifications) suggests that's the right weight for this
/// codebase rather than a new heavy dependency for what's fundamentally a cron job.
///
/// Runs every polling tick as CheckIntervalMinutes describes - a real deployment ticks on the
/// hour; a due report is never more than one tick late, which is fine for a daily/weekly/
/// monthly cadence.
/// </summary>
public class ScheduledReportRunner(IServiceScopeFactory scopeFactory, ILogger<ScheduledReportRunner> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueReportsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // One bad tick (a transient DB blip, etc.) must never kill the whole loop -
                // the next tick tries again.
                logger.LogError(ex, "ScheduledReportRunner tick failed");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
        }
    }

    private async Task RunDueReportsAsync(CancellationToken ct)
    {
        using var scanScope = scopeFactory.CreateScope();
        var scanDb = scanScope.ServiceProvider.GetRequiredService<IAppDbContext>();

        // Cross-tenant by design: this scans every business's due schedules, not one tenant's -
        // see ITenantEntity's own doc comment for why IgnoreQueryFilters needs justifying here.
        // No request/BackgroundJobContext is active yet at this point either, so the normal
        // filter would just silently return zero rows rather than actually scoping correctly.
        var now = DateTimeOffset.UtcNow;
        var dueIds = await scanDb.ScheduledReports
            .IgnoreQueryFilters()
            .Where(r => r.IsActive && r.NextRunAt <= now)
            .Select(r => r.Id)
            .ToListAsync(ct);

        foreach (var id in dueIds)
        {
            if (ct.IsCancellationRequested) break;
            await RunOneAsync(id, ct);
        }
    }

    private async Task RunOneAsync(Guid scheduledReportId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var report = await db.ScheduledReports.IgnoreQueryFilters()
            .Include(r => r.Business)
            .FirstOrDefaultAsync(r => r.Id == scheduledReportId, ct);
        if (report is null) return; // deleted between the scan and now

        var runAt = DateTimeOffset.UtcNow;
        var (from, to) = ScheduledReportScheduling.PeriodCoveredBy(runAt, report.Frequency);

        try
        {
            using (BackgroundJobContext.Push(new BackgroundJobPrincipal(report.CreatedByUserId, report.BusinessId, report.BranchId)))
            {
                var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

                var format = report.Format == Domain.Entities.ReportExportFormat.Pdf
                    ? Application.Common.Interfaces.ReportExportFormat.Pdf
                    : Application.Common.Interfaces.ReportExportFormat.Word;

                var exported = await mediator.Send(new GenerateBusinessReportCommand(from, to, report.BranchId, format), ct);

                foreach (var recipient in report.RecipientEmails.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    await emailSender.SendReportEmailAsync(
                        recipient, report.Business.Name, exported.Content, exported.FileName, exported.ContentType, ct);
                }
            }

            logger.LogInformation(
                "Sent scheduled report {ScheduledReportId} for business {BusinessId} ({From} to {To})",
                report.Id, report.BusinessId, from, to);
        }
        catch (Exception ex)
        {
            // Generation itself failing (not delivery - SesEmailSender already swallows that)
            // must still advance NextRunAt below, or a permanently-broken report would retry
            // every tick forever instead of just skipping to its next real occurrence.
            logger.LogError(ex, "Failed to generate scheduled report {ScheduledReportId}", report.Id);
        }

        report.LastRunAt = runAt;
        report.NextRunAt = ScheduledReportScheduling.NextRunAfter(runAt, report.Frequency);
        await db.SaveChangesAsync(ct);
    }
}
