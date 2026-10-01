namespace ShopKeeper.Api.Tests.Reports;

using ShopKeeper.Application.Reports;
using ShopKeeper.Domain.Entities;

public class ScheduledReportSchedulingTests
{
    [Theory]
    [InlineData(ScheduledReportFrequency.Daily, 1)]
    [InlineData(ScheduledReportFrequency.Weekly, 7)]
    public async Task NextRunAfter_DailyAndWeekly_AddsExactDays(ScheduledReportFrequency frequency, int expectedDays)
    {
        var from = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);
        var next = ScheduledReportScheduling.NextRunAfter(from, frequency, "UTC");
        Assert.Equal(from.AddDays(expectedDays), next);
        await Task.CompletedTask;
    }

    [Fact]
    public void NextRunAfter_Monthly_AddsOneCalendarMonth()
    {
        var from = new DateTimeOffset(2026, 1, 31, 9, 0, 0, TimeSpan.Zero);
        var next = ScheduledReportScheduling.NextRunAfter(from, ScheduledReportFrequency.Monthly, "UTC");
        // .NET's AddMonths clamps to the shorter month's last day, not a fixed 30 days.
        Assert.Equal(new DateTimeOffset(2026, 2, 28, 9, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void NextRunAfter_UnresolvableTimeZone_FallsBackToUtcInsteadOfThrowing()
    {
        // Business.TimeZone had no format validation before this fix - existing rows could hold
        // a value FindSystemTimeZoneById can't resolve. Must degrade to UTC, not crash every
        // tick for that business's schedules forever.
        var from = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);
        var next = ScheduledReportScheduling.NextRunAfter(from, ScheduledReportFrequency.Daily, "not-a-real-timezone");
        Assert.Equal(from.AddDays(1), next);
    }

    [Fact]
    public void PeriodCoveredBy_Daily_IsYesterdayOnly()
    {
        var runAt = new DateTimeOffset(2026, 3, 10, 6, 0, 0, TimeSpan.Zero);
        var (from, to) = ScheduledReportScheduling.PeriodCoveredBy(runAt, ScheduledReportFrequency.Daily, "UTC");
        Assert.Equal(new DateOnly(2026, 3, 9), from);
        Assert.Equal(new DateOnly(2026, 3, 9), to);
    }

    [Fact]
    public void PeriodCoveredBy_Weekly_IsTheFull7DaysEndingYesterday()
    {
        var runAt = new DateTimeOffset(2026, 3, 10, 6, 0, 0, TimeSpan.Zero);
        var (from, to) = ScheduledReportScheduling.PeriodCoveredBy(runAt, ScheduledReportFrequency.Weekly, "UTC");
        Assert.Equal(new DateOnly(2026, 3, 3), from);
        Assert.Equal(new DateOnly(2026, 3, 9), to);
        Assert.Equal(7, to.DayNumber - from.DayNumber + 1);
    }

    [Fact]
    public void PeriodCoveredBy_Monthly_IsTheFullPriorCalendarMonth()
    {
        var runAt = new DateTimeOffset(2026, 3, 1, 6, 0, 0, TimeSpan.Zero);
        var (from, to) = ScheduledReportScheduling.PeriodCoveredBy(runAt, ScheduledReportFrequency.Monthly, "UTC");
        Assert.Equal(new DateOnly(2026, 2, 1), from);
        Assert.Equal(new DateOnly(2026, 2, 28), to);
    }

    [Fact]
    public void PeriodCoveredBy_Daily_UsesBusinessLocalDateNotUtcDate()
    {
        // 2026-03-10 00:30 UTC is still 2026-03-09 16:30 the previous day in
        // America/Los_Angeles (UTC-8, no DST in March until the 8th... using a date past the
        // spring-forward transition, so this is UTC-7/PDT) - a UTC-only calculation would say
        // "yesterday" is the 9th; the business's actual local "yesterday" is the 8th.
        var runAt = new DateTimeOffset(2026, 3, 10, 0, 30, 0, TimeSpan.Zero);
        var (from, to) = ScheduledReportScheduling.PeriodCoveredBy(runAt, ScheduledReportFrequency.Daily, "America/Los_Angeles");
        Assert.Equal(new DateOnly(2026, 3, 8), from);
        Assert.Equal(new DateOnly(2026, 3, 8), to);
    }

    [Fact]
    public void PeriodCoveredBy_Monthly_UsesBusinessLocalMonthNotUtcMonth()
    {
        // 2026-02-28 20:00 UTC is already 2026-03-01 10:00 in Pacific/Kiritimati (a fixed
        // UTC+14 zone, chosen specifically to avoid DST edge cases in this test) - the raw UTC
        // date is still Feb 28, but the business's local calendar has already turned over to
        // March 1, so "the full prior calendar month" from their point of view is the complete
        // Feb 1-28 range. A UTC-only calculation would instead see Feb 28 as "today" and produce
        // Feb 1-27, silently dropping the last day of the month from every monthly report.
        var runAt = new DateTimeOffset(2026, 2, 28, 20, 0, 0, TimeSpan.Zero);
        var (from, to) = ScheduledReportScheduling.PeriodCoveredBy(runAt, ScheduledReportFrequency.Monthly, "Pacific/Kiritimati");
        Assert.Equal(new DateOnly(2026, 2, 1), from);
        Assert.Equal(new DateOnly(2026, 2, 28), to);
    }
}
