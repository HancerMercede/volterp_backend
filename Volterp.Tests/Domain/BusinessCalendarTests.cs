using FluentAssertions;
using Volterp.Domain.Time;
using Xunit;

namespace Volterp.Tests.Domain;

public class BusinessCalendarTests
{
    [Fact]
    public void BusinessOffset_ShouldBeMinusFourHours()
    {
        BusinessCalendar.BusinessOffset.Should().Be(TimeSpan.FromHours(-4));
    }

    [Fact]
    public void FromUtc_ShouldUseTheBusinessDayNotTheUtcDay()
    {
        // 02:00 UTC is 22:00 of the previous business day in UTC-04:00.
        BusinessCalendar.FromUtc(new DateTime(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateOnly(2026, 11, 4));
    }

    [Fact]
    public void FromUtc_ShouldKeepTheDayForBusinessHours()
    {
        BusinessCalendar.FromUtc(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateOnly(2026, 11, 5));
    }

    [Fact]
    public void StartOfBusinessDayUtc_ShouldBeMidnightInTheBusinessTimezone()
    {
        BusinessCalendar.StartOfBusinessDayUtc(new DateOnly(2026, 11, 5))
            .Should().Be(new DateTime(2026, 11, 5, 4, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Today_ShouldMatchFromUtc()
    {
        var utc = new DateTime(2026, 11, 5, 22, 30, 0, DateTimeKind.Utc);

        BusinessCalendar.Today(utc).Should().Be(BusinessCalendar.FromUtc(utc));
    }
}
