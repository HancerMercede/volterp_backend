using System.Globalization;
using FluentAssertions;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.Time;
using Xunit;

namespace Volterp.Tests.Domain;

/// <summary>
/// Independent oracle for the calendar "so far" windows (S2) and the business date (S7).
/// Every expectation is a literal. Nothing is derived from <see cref="ReportingPeriod"/>,
/// so a wrong calendar definition cannot hide behind a circular expectation.
/// </summary>
public class ReportingPeriodCalendarSemanticsTests
{
    private static DateOnly Day(string iso) =>
        DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Theory]
    // today           week from       month from
    [InlineData("2026-11-05", "2026-11-02", "2026-11-01")] // Thursday
    [InlineData("2026-11-02", "2026-11-02", "2026-11-01")] // Monday
    [InlineData("2026-11-01", "2026-10-26", "2026-11-01")] // Sunday
    [InlineData("2026-10-31", "2026-10-26", "2026-10-01")] // Saturday
    [InlineData("2026-03-31", "2026-03-30", "2026-03-01")] // 31-day month
    [InlineData("2026-01-01", "2025-12-29", "2026-01-01")] // first day of the year
    public void Resolve_ShouldReturnTheCalendarWindow(string today, string weekFrom, string monthFrom)
    {
        var day = Day(today);

        var todayScope = ReportingPeriod.Resolve(DashboardScope.Today, day);
        todayScope.From.Should().Be(day);
        todayScope.To.Should().Be(day);

        var weekScope = ReportingPeriod.Resolve(DashboardScope.Week, day);
        weekScope.From.Should().Be(Day(weekFrom));
        weekScope.To.Should().Be(day);

        var monthScope = ReportingPeriod.Resolve(DashboardScope.Month, day);
        monthScope.From.Should().Be(Day(monthFrom));
        monthScope.To.Should().Be(day);

        // The chart's "Semana" and the pill's "Esta semana" are the same window.
        // The same holds for "Mes" and "Este mes".
        ReportingPeriod.Resolve(ChartWindow.Week, day).Should().Be(ReportingPeriod.Resolve(DashboardScope.Week, day));
        ReportingPeriod.Resolve(ChartWindow.Month, day).Should().Be(ReportingPeriod.Resolve(DashboardScope.Month, day));
    }

    [Fact]
    public void Resolve_YearWindow_ShouldStartOnJanuaryFirst()
    {
        var year = ReportingPeriod.Resolve(ChartWindow.Year, Day("2026-11-05"));

        year.From.Should().Be(Day("2026-01-01"));
        year.To.Should().Be(Day("2026-11-05"));
    }

    [Theory]
    [InlineData("2026-11-05", 4)] // Monday..Thursday
    [InlineData("2026-11-01", 7)] // Monday..Sunday
    [InlineData("2026-11-02", 1)] // Monday
    public void Buckets_WeekWindow_ShouldBeOnePerDay(string today, int expected)
    {
        var buckets = ReportingPeriod.Buckets(ChartWindow.Week, Day(today));

        buckets.Should().HaveCount(expected);
        buckets.Should().OnlyContain(b => b.Unit == ChartBucketUnit.Day);
    }

    [Theory]
    [InlineData("2026-11-05", 5)]
    [InlineData("2026-10-31", 31)]
    public void Buckets_MonthWindow_ShouldBeOnePerDayOfTheMonth(string today, int expected)
    {
        var buckets = ReportingPeriod.Buckets(ChartWindow.Month, Day(today));

        buckets.Should().HaveCount(expected);
        buckets.Should().OnlyContain(b => b.Unit == ChartBucketUnit.Day);
    }

    [Theory]
    [InlineData("2026-11-05", 11)]
    [InlineData("2026-01-01", 1)]
    [InlineData("2026-12-31", 12)]
    public void Buckets_YearWindow_ShouldBeOnePerMonth(string today, int expected)
    {
        var buckets = ReportingPeriod.Buckets(ChartWindow.Year, Day(today));

        buckets.Should().HaveCount(expected);
        buckets.Should().OnlyContain(b => b.Unit == ChartBucketUnit.Month);
    }

    [Fact]
    public void Previous_MonthScope_ShouldCoverTheSameDaysOfThePreviousMonth()
    {
        var previous = ReportingPeriod.Previous(DashboardScope.Month, Day("2026-11-05"));

        previous.From.Should().Be(Day("2026-10-01"));
        previous.To.Should().Be(Day("2026-10-05"));
    }

    [Fact]
    public void Previous_MonthScope_ShouldNotRunPastTheEndOfAShorterPreviousMonth()
    {
        var previous = ReportingPeriod.Previous(DashboardScope.Month, Day("2026-03-31"));

        previous.From.Should().Be(Day("2026-02-01"));
        previous.To.Should().Be(Day("2026-02-28"));
    }

    [Fact]
    public void Previous_WeekScope_ShouldCoverTheSameWeekdaysOneWeekEarlier()
    {
        var previous = ReportingPeriod.Previous(DashboardScope.Week, Day("2026-11-05"));

        previous.From.Should().Be(Day("2026-10-26"));
        previous.To.Should().Be(Day("2026-10-29"));
    }

    [Fact]
    public void BusinessDate_OfASaleMadeAtTenInTheEveningLocal_IsThatLocalDay()
    {
        // 02:00 UTC is 22:00 of the previous business day in UTC-04:00.
        BusinessCalendar.FromUtc(new DateTime(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc))
            .Should().Be(Day("2026-11-04"));
    }

    [Fact]
    public void BusinessDate_OfANewRow_IsTheBusinessDayOfItsOwnCreatedAt()
    {
        var sale = new Sale();

        // The entity stamps both fields from their own clock reading, so they can
        // differ by a fraction of a millisecond. The business day must be the same.
        var earliest = BusinessCalendar.FromUtc(sale.CreatedAt.AddSeconds(-1));
        var latest = BusinessCalendar.FromUtc(sale.CreatedAt.AddSeconds(1));

        sale.BusinessDate.Should().BeOnOrAfter(earliest);
        sale.BusinessDate.Should().BeOnOrBefore(latest);
    }
}
