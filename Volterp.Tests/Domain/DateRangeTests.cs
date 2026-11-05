using FluentAssertions;
using Volterp.Domain.ValueObjects;
using Xunit;

namespace Volterp.Tests.Domain;

public class DateRangeTests
{
    [Fact]
    public void DayCount_ShouldCountBothEnds()
    {
        var range = new DateRange(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));

        range.DayCount.Should().Be(5);
    }

    [Fact]
    public void DayCount_ShouldBeOneForASingleDay()
    {
        var range = new DateRange(new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 5));

        range.DayCount.Should().Be(1);
    }

    [Fact]
    public void Contains_ShouldIncludeBothEnds()
    {
        var range = new DateRange(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));

        range.Contains(new DateOnly(2026, 11, 1)).Should().BeTrue();
        range.Contains(new DateOnly(2026, 11, 5)).Should().BeTrue();
        range.Contains(new DateOnly(2026, 10, 31)).Should().BeFalse();
        range.Contains(new DateOnly(2026, 11, 6)).Should().BeFalse();
    }

    [Fact]
    public void Days_ShouldEnumerateEveryDayInOrder()
    {
        var range = new DateRange(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 3));

        range.Days().Should().Equal(
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 2),
            new DateOnly(2026, 11, 3));
    }

    [Fact]
    public void Constructor_ShouldRejectAnEndBeforeItsStart()
    {
        var act = () => new DateRange(new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 1));

        act.Should().Throw<ArgumentException>();
    }
}
