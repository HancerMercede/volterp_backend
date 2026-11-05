using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;

namespace Volterp.Domain.Time;

/// <summary>
/// Resolves the dashboard scopes and the chart windows into concrete business-date
/// ranges, and into the preceding period the change percentages compare against.
/// </summary>
/// <remarks>
/// Every window is a calendar period measured "so far": the current week runs from
/// Monday up to today, the current month from its first day up to today, and the
/// current year from January up to today. A label such as "Este mes" has to mean
/// the calendar month, never a rolling window of thirty days.
/// </remarks>
public static class ReportingPeriod
{
    public static DateRange Resolve(DashboardScope scope, DateOnly today) => scope switch
    {
        DashboardScope.Today => new DateRange(today, today),
        DashboardScope.Week => new DateRange(StartOfWeek(today), today),
        DashboardScope.Month => new DateRange(StartOfMonth(today), today),
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown dashboard scope.")
    };

    public static DateRange Resolve(ChartWindow window, DateOnly today) => window switch
    {
        ChartWindow.Week => new DateRange(StartOfWeek(today), today),
        ChartWindow.Month => new DateRange(StartOfMonth(today), today),
        ChartWindow.Year => new DateRange(StartOfYear(today), today),
        _ => throw new ArgumentOutOfRangeException(nameof(window), window, "Unknown chart window.")
    };

    /// <summary>
    /// The period one calendar unit back, covering the same elapsed days.
    /// </summary>
    public static DateRange Previous(DashboardScope scope, DateOnly today) => scope switch
    {
        DashboardScope.Today => new DateRange(today.AddDays(-1), today.AddDays(-1)),
        DashboardScope.Week => PreviousWeek(today),
        DashboardScope.Month => PreviousMonth(today),
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown dashboard scope.")
    };

    /// <summary>
    /// The period one calendar unit back, covering the same elapsed days.
    /// </summary>
    public static DateRange Previous(ChartWindow window, DateOnly today) => window switch
    {
        ChartWindow.Week => PreviousWeek(today),
        ChartWindow.Month => PreviousMonth(today),
        ChartWindow.Year => PreviousYear(today),
        _ => throw new ArgumentOutOfRangeException(nameof(window), window, "Unknown chart window.")
    };

    /// <summary>
    /// The buckets the chart series is made of. A window measured in days produces
    /// one bucket per day; a window measured in a year produces one bucket per
    /// month.
    /// </summary>
    public static IReadOnlyList<ChartBucket> Buckets(ChartWindow window, DateOnly today)
    {
        var range = Resolve(window, today);

        if (window == ChartWindow.Year)
        {
            var months = (range.To.Year - range.From.Year) * 12 + range.To.Month - range.From.Month + 1;

            return Enumerable.Range(0, months)
                .Select(i => new ChartBucket(ChartBucketUnit.Month, range.From.AddMonths(i)))
                .ToList();
        }

        return range.Days()
            .Select(day => new ChartBucket(ChartBucketUnit.Day, day))
            .ToList();
    }

    /// <summary>The Monday of the week the given date belongs to.</summary>
    public static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;

        return date.AddDays(-daysSinceMonday);
    }

    public static DateOnly StartOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    public static DateOnly StartOfYear(DateOnly date) => new(date.Year, 1, 1);

    private static DateRange PreviousWeek(DateOnly today)
    {
        var thisWeek = StartOfWeek(today);
        var elapsedDays = today.DayNumber - thisWeek.DayNumber;
        var previousWeek = thisWeek.AddDays(-7);

        return new DateRange(previousWeek, previousWeek.AddDays(elapsedDays));
    }

    private static DateRange PreviousMonth(DateOnly today)
    {
        var thisMonth = StartOfMonth(today);
        var elapsedDays = today.DayNumber - thisMonth.DayNumber;
        var previousMonth = thisMonth.AddMonths(-1);
        var lastDayOfPreviousMonth = new DateOnly(
            previousMonth.Year,
            previousMonth.Month,
            DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month));

        var end = previousMonth.AddDays(elapsedDays);
        if (end > lastDayOfPreviousMonth)
            end = lastDayOfPreviousMonth;

        return new DateRange(previousMonth, end);
    }

    private static DateRange PreviousYear(DateOnly today)
    {
        var thisYear = StartOfYear(today);
        var elapsedDays = today.DayNumber - thisYear.DayNumber;
        var previousYear = thisYear.AddYears(-1);
        var lastDayOfPreviousYear = new DateOnly(previousYear.Year, 12, 31);

        var end = previousYear.AddDays(elapsedDays);
        if (end > lastDayOfPreviousYear)
            end = lastDayOfPreviousYear;

        return new DateRange(previousYear, end);
    }
}
