using System.Globalization;
using FluentAssertions;
using Volterp.Domain.Enums;
using Volterp.Domain.Time;
using Xunit;

namespace Volterp.Tests.Domain;

public class ReportingPeriodTests
{
    // 2026-11-05 is a Thursday, so its week starts on Monday 2026-11-02.
    private static readonly DateOnly Thursday = new(2026, 11, 5);

    [Fact]
    public void Resolve_TodayScope_ShouldCoverOnlyToday()
    {
        var range = ReportingPeriod.Resolve(DashboardScope.Today, Thursday);

        range.From.Should().Be(Thursday);
        range.To.Should().Be(Thursday);
    }

    [Fact]
    public void Resolve_WeekScope_ShouldStartOnMonday()
    {
        var range = ReportingPeriod.Resolve(DashboardScope.Week, Thursday);

        range.From.Should().Be(new DateOnly(2026, 11, 2));
        range.To.Should().Be(Thursday);
    }

    [Fact]
    public void Resolve_WeekScope_OnMonday_ShouldCoverOnlyThatDay()
    {
        var monday = new DateOnly(2026, 11, 2);

        var range = ReportingPeriod.Resolve(DashboardScope.Week, monday);

        range.From.Should().Be(monday);
        range.To.Should().Be(monday);
    }

    [Fact]
    public void Resolve_WeekScope_OnSunday_ShouldCoverTheWholeWeek()
    {
        var sunday = new DateOnly(2026, 11, 8);

        var range = ReportingPeriod.Resolve(DashboardScope.Week, sunday);

        range.From.Should().Be(new DateOnly(2026, 11, 2));
        range.To.Should().Be(sunday);
        range.DayCount.Should().Be(7);
    }

    [Fact]
    public void Resolve_MonthScope_ShouldStartOnTheFirstDayOfTheMonth()
    {
        var range = ReportingPeriod.Resolve(DashboardScope.Month, Thursday);

        range.From.Should().Be(new DateOnly(2026, 11, 1));
        range.To.Should().Be(Thursday);
    }

    [Fact]
    public void Resolve_ChartWeek_ShouldMatchTheDashboardWeekScope()
    {
        ReportingPeriod.Resolve(ChartWindow.Week, Thursday)
            .Should().Be(ReportingPeriod.Resolve(DashboardScope.Week, Thursday));
    }

    [Fact]
    public void Resolve_ChartMonth_ShouldMatchTheDashboardMonthScope()
    {
        ReportingPeriod.Resolve(ChartWindow.Month, Thursday)
            .Should().Be(ReportingPeriod.Resolve(DashboardScope.Month, Thursday));
    }

    [Fact]
    public void Resolve_ChartYear_ShouldStartOnJanuaryFirst()
    {
        var range = ReportingPeriod.Resolve(ChartWindow.Year, Thursday);

        range.From.Should().Be(new DateOnly(2026, 1, 1));
        range.To.Should().Be(Thursday);
    }

    [Fact]
    public void Previous_TodayScope_ShouldBeYesterday()
    {
        var range = ReportingPeriod.Previous(DashboardScope.Today, Thursday);

        range.From.Should().Be(new DateOnly(2026, 11, 4));
        range.To.Should().Be(new DateOnly(2026, 11, 4));
    }

    [Fact]
    public void Previous_WeekScope_ShouldCoverTheSameWeekdaysOneWeekEarlier()
    {
        var range = ReportingPeriod.Previous(DashboardScope.Week, Thursday);

        range.From.Should().Be(new DateOnly(2026, 10, 26));
        range.To.Should().Be(new DateOnly(2026, 10, 29));
    }

    [Fact]
    public void Previous_MonthScope_ShouldCoverTheSameDaysOfThePreviousMonth()
    {
        var range = ReportingPeriod.Previous(DashboardScope.Month, Thursday);

        range.From.Should().Be(new DateOnly(2026, 10, 1));
        range.To.Should().Be(new DateOnly(2026, 10, 5));
    }

    [Fact]
    public void Previous_MonthScope_ShouldNotRunPastTheEndOfTheShorterPreviousMonth()
    {
        var march = new DateOnly(2026, 3, 31);

        var range = ReportingPeriod.Previous(DashboardScope.Month, march);

        range.From.Should().Be(new DateOnly(2026, 2, 1));
        range.To.Should().Be(new DateOnly(2026, 2, 28));
    }

    [Fact]
    public void Previous_YearScope_ShouldCoverTheSameDaysOfThePreviousYear()
    {
        var range = ReportingPeriod.Previous(ChartWindow.Year, Thursday);

        range.From.Should().Be(new DateOnly(2025, 1, 1));
        range.To.Should().Be(new DateOnly(2025, 11, 5));
    }

    [Fact]
    public void Buckets_ChartWeek_ShouldBeOnePerDay()
    {
        var buckets = ReportingPeriod.Buckets(ChartWindow.Week, Thursday);

        buckets.Should().HaveCount(4);
        buckets.Should().AllSatisfy(b => b.Unit.Should().Be(ChartBucketUnit.Day));
        buckets.Select(b => b.Start).Should().Equal(
            new DateOnly(2026, 11, 2),
            new DateOnly(2026, 11, 3),
            new DateOnly(2026, 11, 4),
            new DateOnly(2026, 11, 5));
    }

    [Fact]
    public void Buckets_ChartMonth_ShouldBeOnePerDayOfTheMonth()
    {
        var buckets = ReportingPeriod.Buckets(ChartWindow.Month, Thursday);

        buckets.Should().HaveCount(5);
        buckets.First().Start.Should().Be(new DateOnly(2026, 11, 1));
        buckets.Last().Start.Should().Be(Thursday);
    }

    [Fact]
    public void Buckets_ChartYear_ShouldBeOnePerMonthOfTheYear()
    {
        var buckets = ReportingPeriod.Buckets(ChartWindow.Year, Thursday);

        buckets.Should().HaveCount(11);
        buckets.Should().AllSatisfy(b => b.Unit.Should().Be(ChartBucketUnit.Month));
        buckets.First().Start.Should().Be(new DateOnly(2026, 1, 1));
        buckets.Last().Start.Should().Be(new DateOnly(2026, 11, 1));
    }

    [Theory]
    // Every day of a week, the month edge cases and the year edge case. The
    // expectations are literals on purpose: an expectation derived from
    // ReportingPeriod itself cannot detect a wrong calendar definition.
    [InlineData("2026-10-26", "2026-10-26", "2026-10-01")] // Monday
    [InlineData("2026-10-31", "2026-10-26", "2026-10-01")] // Saturday, last month day
    [InlineData("2026-11-01", "2026-10-26", "2026-11-01")] // Sunday
    [InlineData("2026-03-31", "2026-03-30", "2026-03-01")] // Tuesday, 31-day month
    [InlineData("2026-01-01", "2025-12-29", "2026-01-01")] // Thursday, first day of the year
    public void Resolve_ShouldReturnTheCalendarWindowOnAnyDay(string today, string weekFrom, string monthFrom)
    {
        var day = DateOnly.ParseExact(today, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        ReportingPeriod.Resolve(DashboardScope.Week, day).From
            .Should().Be(DateOnly.ParseExact(weekFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        ReportingPeriod.Resolve(DashboardScope.Week, day).To.Should().Be(day);

        ReportingPeriod.Resolve(DashboardScope.Month, day).From
            .Should().Be(DateOnly.ParseExact(monthFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        ReportingPeriod.Resolve(DashboardScope.Month, day).To.Should().Be(day);
    }

    [Theory]
    [InlineData("2026-11-05", 4)] // Monday..Thursday
    [InlineData("2026-11-01", 7)] // Monday..Sunday
    [InlineData("2026-11-02", 1)] // Monday
    public void Buckets_WeekWindow_ShouldBeOnePerDay(string today, int expected)
    {
        var day = DateOnly.ParseExact(today, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var buckets = ReportingPeriod.Buckets(ChartWindow.Week, day);

        buckets.Should().HaveCount(expected);
        buckets.Should().AllSatisfy(b => b.Unit.Should().Be(ChartBucketUnit.Day));
    }

    [Theory]
    [InlineData("2026-11-05", 5)]
    [InlineData("2026-10-31", 31)]
    public void Buckets_MonthWindow_ShouldBeOnePerDayOfTheMonth(string today, int expected)
    {
        var day = DateOnly.ParseExact(today, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var buckets = ReportingPeriod.Buckets(ChartWindow.Month, day);

        buckets.Should().HaveCount(expected);
        buckets.Should().AllSatisfy(b => b.Unit.Should().Be(ChartBucketUnit.Day));
    }

    [Theory]
    [InlineData("2026-11-05", 11)]
    [InlineData("2026-01-01", 1)]
    [InlineData("2026-12-31", 12)]
    public void Buckets_YearWindow_ShouldBeOnePerMonth(string today, int expected)
    {
        var day = DateOnly.ParseExact(today, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var buckets = ReportingPeriod.Buckets(ChartWindow.Year, day);

        buckets.Should().HaveCount(expected);
        buckets.Should().AllSatisfy(b => b.Unit.Should().Be(ChartBucketUnit.Month));
    }
}
