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
}
