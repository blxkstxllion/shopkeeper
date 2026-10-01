namespace ShopKeeper.Application.Reports;

using ShopKeeper.Domain.Entities;

/// <summary>Shared by CreateScheduledReportCommand (first NextRunAt) and ScheduledReportRunner
/// (the next one after each run) so both compute occurrences the same way.</summary>
public static class ScheduledReportScheduling
{
    /// <summary>Every calculation below happens in the business's own local calendar, not UTC -
    /// "yesterday", "the last 7 days", and "the 1st of the month" all mean the business owner's
    /// local date, not whatever date the UTC clock happens to be on when the runner ticks. A
    /// business more than a few hours off UTC would otherwise get systematically wrong period
    /// boundaries (a "daily" report whose "yesterday" cutoff lands mid-afternoon their time).
    /// timeZoneId is the Business.TimeZone field, which has no format validation older than this
    /// fix (see UpdateBusinessProfileCommandValidator) and even after it, existing rows created
    /// before that validation existed could still hold a bad value - falling back to UTC on an
    /// unresolvable id keeps a business with bad data working exactly as before, rather than
    /// breaking their scheduled reports outright.</summary>
    public static DateTimeOffset NextRunAfter(DateTimeOffset from, ScheduledReportFrequency frequency, string timeZoneId)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var localFrom = TimeZoneInfo.ConvertTime(from, tz);
        var localNext = frequency switch
        {
            ScheduledReportFrequency.Daily => localFrom.AddDays(1),
            ScheduledReportFrequency.Weekly => localFrom.AddDays(7),
            ScheduledReportFrequency.Monthly => localFrom.AddMonths(1),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, null),
        };
        return TimeZoneInfo.ConvertTimeToUtc(localNext.DateTime, tz);
    }

    /// <summary>The date range a run at `runAt` should cover for `frequency` - the period that
    /// just elapsed (e.g. a Monday-morning Weekly run covers the 7 days ending yesterday), not
    /// the period still in progress. See NextRunAfter's doc comment on why this is computed in
    /// the business's local calendar rather than UTC.</summary>
    public static (DateOnly From, DateOnly To) PeriodCoveredBy(DateTimeOffset runAt, ScheduledReportFrequency frequency, string timeZoneId)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var localRunAt = TimeZoneInfo.ConvertTime(runAt, tz);
        var to = DateOnly.FromDateTime(localRunAt.DateTime).AddDays(-1);
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

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
