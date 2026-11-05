namespace Volterp.Domain.ValueObjects;

/// <summary>
/// An inclusive range of business dates.
/// </summary>
public readonly record struct DateRange
{
    public DateOnly From { get; }
    public DateOnly To { get; }

    public DateRange(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new ArgumentException(
                $"Date range end {to:yyyy-MM-dd} precedes its start {from:yyyy-MM-dd}.",
                nameof(to));

        From = from;
        To = to;
    }

    /// <summary>Number of business days the range covers.</summary>
    public int DayCount => To.DayNumber - From.DayNumber + 1;

    public bool Contains(DateOnly date) => date >= From && date <= To;

    /// <summary>
    /// Every business date in the range, in ascending order.
    /// </summary>
    public IEnumerable<DateOnly> Days()
    {
        for (var day = From; day <= To; day = day.AddDays(1))
            yield return day;
    }

    public override string ToString() => $"{From:yyyy-MM-dd}..{To:yyyy-MM-dd}";
}
