namespace Volterp.Domain.Enums;

/// <summary>
/// Time scope applied to every flow metric on the dashboard.
/// </summary>
public enum DashboardScope
{
    /// <summary>Only the current business day.</summary>
    Today,

    /// <summary>The current business week, from Monday up to today.</summary>
    Week,

    /// <summary>The current business month, from its first day up to today.</summary>
    Month
}
