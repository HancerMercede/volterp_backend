namespace Volterp.Application.DTOs;

public record DashboardDataDto(
    decimal Ventas,
    decimal Compras,
    int Clientes,
    decimal Utilidad
);

public record SalesChartDto(
    string Day,
    decimal Sales,
    decimal Purchases
);

public record CategoryDataDto(
    string Name,
    int Percent,
    string Color
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
    int Count,
    string BadgeColor
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
