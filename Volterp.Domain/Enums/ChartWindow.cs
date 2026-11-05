namespace Volterp.Domain.Enums;

/// <summary>
/// Time window applied to the "Ventas vs Compras" chart, independently of the
/// dashboard scope.
/// </summary>
public enum ChartWindow
{
    /// <summary>The current business week, from Monday up to today.</summary>
    Week,

    /// <summary>The current business month, from its first day up to today.</summary>
    Month,

    /// <summary>The current business year, from January up to today.</summary>
    Year
}
