namespace Volterp.Domain.Time;

/// <summary>
/// The business calendar of the tenant: which calendar day a UTC instant belongs to.
/// </summary>
/// <remarks>
/// The Dominican Republic abolished daylight saving time in 2020, so UTC-04:00
/// applies to every business date this system handles. A fixed offset is used
/// instead of <see cref="TimeZoneInfo"/> so results do not depend on an IANA or
/// Windows time-zone id being resolvable on the host.
/// </remarks>
public static class BusinessCalendar
{
    /// <summary>Offset of the business timezone from UTC.</summary>
    public static readonly TimeSpan BusinessOffset = TimeSpan.FromHours(-4);

    /// <summary>The business date a UTC instant falls on.</summary>
    public static DateOnly FromUtc(DateTime utc) => DateOnly.FromDateTime(utc + BusinessOffset);

    /// <summary>The current business date.</summary>
    public static DateOnly Today(DateTime utc) => FromUtc(utc);

    /// <summary>
    /// The UTC instant at which the given business date starts.
    /// </summary>
    public static DateTime StartOfBusinessDayUtc(DateOnly businessDate) =>
        businessDate.ToDateTime(TimeOnly.MinValue) - BusinessOffset;
}
