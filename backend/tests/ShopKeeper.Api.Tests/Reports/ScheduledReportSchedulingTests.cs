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
        var next = ScheduledReportScheduling.NextRunAfter(from, frequency);
        Assert.Equal(from.AddDays(expectedDays), next);
        await Task.CompletedTask;
    }

    [Fact]
    public void NextRunAfter_Monthly_AddsOneCalendarMonth()
    {
        var from = new DateTimeOffset(2026, 1, 31, 9, 0, 0, TimeSpan.Zero);
        var next = ScheduledReportScheduling.NextRunAfter(from, ScheduledReportFrequency.Monthly);
        // .NET's AddMonths clamps to the shorter month's last day, not a fixed 30 days.
        Assert.Equal(new DateTimeOffset(2026, 2, 28, 9, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void PeriodCoveredBy_Daily_IsYesterdayOnly()
    {
        var runAt = new DateTimeOffset(2026, 3, 10, 6, 0, 0, TimeSpan.Zero);
        var (from, to) = ScheduledReportScheduling.PeriodCoveredBy(runAt, ScheduledReportFrequency.Daily);
        Assert.Equal(new DateOnly(2026, 3, 9), from);
        Assert.Equal(new DateOnly(2026, 3, 9), to);
    }

    [Fact]
    public void PeriodCoveredBy_Weekly_IsTheFull7DaysEndingYesterday()
    {
        var runAt = new DateTimeOffset(2026, 3, 10, 6, 0, 0, TimeSpan.Zero);
        var (from, to) = ScheduledReportScheduling.PeriodCoveredBy(runAt, ScheduledReportFrequency.Weekly);
        Assert.Equal(new DateOnly(2026, 3, 3), from);
        Assert.Equal(new DateOnly(2026, 3, 9), to);
        Assert.Equal(7, to.DayNumber - from.DayNumber + 1);
    }

    [Fact]
    public void PeriodCoveredBy_Monthly_IsTheFullPriorCalendarMonth()
    {
        var runAt = new DateTimeOffset(2026, 3, 1, 6, 0, 0, TimeSpan.Zero);
        var (from, to) = ScheduledReportScheduling.PeriodCoveredBy(runAt, ScheduledReportFrequency.Monthly);
        Assert.Equal(new DateOnly(2026, 2, 1), from);
        Assert.Equal(new DateOnly(2026, 2, 28), to);
    }
}
