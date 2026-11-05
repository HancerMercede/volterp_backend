namespace Volterp.Domain.Time;

/// <summary>
/// One point of the "Ventas vs Compras" series: the calendar unit it aggregates
/// and the first business date that unit starts on.
/// </summary>
public sealed record ChartBucket(ChartBucketUnit Unit, DateOnly Start);
