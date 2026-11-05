namespace Volterp.Domain.Time;

/// <summary>
/// The calendar unit a chart bucket aggregates.
/// </summary>
public enum ChartBucketUnit
{
    /// <summary>One bucket per business day.</summary>
    Day,

    /// <summary>One bucket per business month.</summary>
    Month
}
