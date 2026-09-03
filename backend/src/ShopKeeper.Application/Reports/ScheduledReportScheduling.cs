namespace ShopKeeper.Application.Reports;

using ShopKeeper.Domain.Entities;

/// <summary>Shared by CreateScheduledReportCommand (first NextRunAt) and ScheduledReportRunner
/// (the next one after each run) so both compute occurrences the same way.</summary>
public static class ScheduledReportScheduling
{
    public static DateTimeOffset NextRunAfter(DateTimeOffset from, ScheduledReportFrequency frequency) => frequency switch
    {
        ScheduledReportFrequency.Daily => from.AddDays(1),
        ScheduledReportFrequency.Weekly => from.AddDays(7),
        ScheduledReportFrequency.Monthly => from.AddMonths(1),
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, null),
    };

    /// <summary>The date range a run at `runAt` should cover for `frequency` - the period that
    /// just elapsed (e.g. a Monday-morning Weekly run covers the 7 days ending yesterday), not
    /// the period still in progress.</summary>
    public static (DateOnly From, DateOnly To) PeriodCoveredBy(DateTimeOffset runAt, ScheduledReportFrequency frequency)
    {
        var to = DateOnly.FromDateTime(runAt.UtcDateTime).AddDays(-1);
        var from = frequency switch
        {
            ScheduledReportFrequency.Daily => to,
            ScheduledReportFrequency.Weekly => to.AddDays(-6),
            // The first day of `to`'s month, not "one month before `to`, plus a day" - that
            // formula silently breaks whenever the run doesn't land on the 1st, e.g. `to` of
            // Feb 28 would produce Jan 29 instead of Feb 1 (a real bug this exact test caught).
            ScheduledReportFrequency.Monthly => new DateOnly(to.Year, to.Month, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, null),
        };
        return (from, to);
    }
}
