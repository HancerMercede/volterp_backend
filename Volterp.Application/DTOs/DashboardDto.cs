namespace Volterp.Application.DTOs;

public record DashboardDataDto(
    decimal Ventas,
    decimal Compras,
    int Clientes,
    decimal Utilidad,
    decimal? VentasChangePercent,
    decimal? ComprasChangePercent,
    decimal? ClientesChangePercent,
    decimal? UtilidadChangePercent);

public record SalesChartDto(
    // Carries the label of the bucket: the day name for a daily bucket, the short
    // month name for a monthly one. The field keeps its name for compatibility.
    string Day,
    decimal Sales,
    decimal Purchases
);

public record CategoryDataDto(
    string Name,
    int Percent,
    string Color,
    // The amount of the category inside the window: the donut total comes from the
    // response, the client never hardcodes it.
    decimal Total
);

public record TopProductDto(
    string Name,
    string Category,
    int Sales,
    decimal Value,
    string ImageUrl,
    string RankColor
);

public record ActivityDto(
    string Text,
    string Time,
    string Type
);

public record ReminderDto(
    string Text,
    // A count for the count reminders, the real amount to collect for the receivables one.
    decimal Count,
    string BadgeColor,
    // True when the value is an amount to collect rather than a count of items.
    bool IsAmount = false
);

public record DashboardStatsDto(
    int OrdersCompleted,
    int OrdersPending,
    int NewProducts,
    int ActiveSuppliers
);

public record DashboardResponse(
    DashboardDataDto Data,
    List<SalesChartDto> SalesChart,
    List<CategoryDataDto> Categories,
    List<TopProductDto> TopProducts,
    List<ActivityDto> Activities,
    List<ReminderDto> Reminders,
    DashboardStatsDto Stats
);
