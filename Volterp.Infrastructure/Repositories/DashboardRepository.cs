using Microsoft.EntityFrameworkCore;
using Volterp.Application.DTOs;
using Volterp.Application.Interfaces;
using Volterp.Domain.Enums;
using Volterp.Domain.Time;
using Volterp.Domain.ValueObjects;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class DashboardRepository(VolterpDbContext context) : IDashboardRepository
{
    private static readonly string[] DayLabels = ["Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb"];
    private static readonly string[] MonthLabels = ["Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"];
    private static readonly string[] CategoryColors = ["#FACC15", "#6366F1", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#EC4899", "#14B8A6"];
    private static readonly string[] RankColors = ["#FACC15", "#6366F1", "#10B981", "#F59E0B", "#EF4444"];

    public async Task<DashboardResponse> GetDashboardDataAsync(
        DashboardScope scope,
        ChartWindow chartWindow,
        CancellationToken ct = default)
    {
        var today = BusinessCalendar.Today(DateTime.UtcNow);

        var range = ReportingPeriod.Resolve(scope, today);
        var previousRange = ReportingPeriod.Previous(scope, today);

        var ventas = await SumCompletedSalesAsync(range, ct);
        var compras = await SumCompletedPurchasesAsync(range, ct);
        var totalClientes = await CountNewClientsAsync(range, ct);

        var previousVentas = await SumCompletedSalesAsync(previousRange, ct);
        var previousCompras = await SumCompletedPurchasesAsync(previousRange, ct);
        var previousClientes = await CountNewClientsAsync(previousRange, ct);

        var salesChartTask = await GetSalesChartAsync(chartWindow, today, ct);
        var categoriesTask = await GetCategoryDataAsync(range, ct);
        var topProductsTask = await GetTopProductsAsync(range, ct);
        var activitiesTask = await GetRecentActivitiesAsync(ct);
        var remindersTask = await GetRemindersAsync(ct);
        var statsTask = await GetDashboardStatsAsync(range, ct);


        return new DashboardResponse(
            Data: new DashboardDataDto(
                Ventas: ventas,
                Compras: compras,
                Clientes: totalClientes,
                Utilidad: ventas - compras,
                VentasChangePercent: ChangePercent(ventas, previousVentas),
                ComprasChangePercent: ChangePercent(compras, previousCompras),
                ClientesChangePercent: ChangePercent(totalClientes, previousClientes),
                UtilidadChangePercent: ChangePercent(ventas - compras, previousVentas - previousCompras)
            ),
            SalesChart: salesChartTask,
            Categories: categoriesTask,
            TopProducts: topProductsTask,
            Activities: activitiesTask,
            Reminders: remindersTask,
            Stats: statsTask
        );
    }

    // The sales of the window: the completed ones dated inside it.
    private async Task<decimal> SumCompletedSalesAsync(DateRange range, CancellationToken ct)
    {
        return await context.Sales
            .Where(s => s.Status == SaleStatus.Completed
                        && s.BusinessDate >= range.From
                        && s.BusinessDate <= range.To)
            .SumAsync(s => (decimal?)s.Total, ct) ?? 0;
    }

    // The purchases of the window: the completed ones dated inside it.
    private async Task<decimal> SumCompletedPurchasesAsync(DateRange range, CancellationToken ct)
    {
        return await context.Purchases
            .Where(p => p.Status == EntityStatus.Completed
                        && p.BusinessDate >= range.From
                        && p.BusinessDate <= range.To)
            .SumAsync(p => (decimal?)p.Total, ct) ?? 0;
    }

    // The clients registered inside the window. "Clientes activos" used to count every
    // client ever created, which is not what the card claims.
    private async Task<int> CountNewClientsAsync(DateRange range, CancellationToken ct)
    {
        return await context.Clients
            .CountAsync(c => c.BusinessDate >= range.From && c.BusinessDate <= range.To, ct);
    }

    // The change of a metric against the preceding period. A null means the comparison
    // is undefined: the previous period had no value to compare against. It is never
    // coerced to zero.
    private static decimal? ChangePercent(decimal current, decimal previous)
        => previous == 0 ? null : (current - previous) / previous * 100m;

    private async Task<List<SalesChartDto>> GetSalesChartAsync(ChartWindow chartWindow, DateOnly today, CancellationToken ct)
    {
        var range = ReportingPeriod.Resolve(chartWindow, today);
        var buckets = ReportingPeriod.Buckets(chartWindow, today);

        // Daily aggregates of the window, on the business date
        var dailySales = await context.Sales
            .Where(s => s.Status == SaleStatus.Completed
                        && s.BusinessDate >= range.From
                        && s.BusinessDate <= range.To)
            .GroupBy(s => s.BusinessDate)
            .Select(g => new { Date = g.Key, Total = g.Sum(s => s.Total) })
            .ToListAsync(ct);

        var dailyPurchases = await context.Purchases
            .Where(p => p.Status == EntityStatus.Completed
                        && p.BusinessDate >= range.From
                        && p.BusinessDate <= range.To)
            .GroupBy(p => p.BusinessDate)
            .Select(g => new { Date = g.Key, Total = g.Sum(p => p.Total) })
            .ToListAsync(ct);

        var salesLookup = ToBucketLookup(dailySales.ToDictionary(s => s.Date, s => s.Total), chartWindow);
        var purchasesLookup = ToBucketLookup(dailyPurchases.ToDictionary(p => p.Date, p => p.Total), chartWindow);

        // Every bucket of the window is present: a gap in the series is a lie, a zero is a fact
        return buckets
            .Select(bucket => new SalesChartDto(
                BucketLabel(bucket),
                salesLookup.GetValueOrDefault(bucket.Start, 0),
                purchasesLookup.GetValueOrDefault(bucket.Start, 0)
            ))
            .ToList();
    }

    // Folds the daily totals into the buckets of the window: one bucket per day, except
    // for the yearly window, which holds one bucket per month.
    private static Dictionary<DateOnly, decimal> ToBucketLookup(
        Dictionary<DateOnly, decimal> dailyTotals,
        ChartWindow chartWindow)
    {
        if (chartWindow != ChartWindow.Year)
            return dailyTotals;

        return dailyTotals
            .GroupBy(total => new DateOnly(total.Key.Year, total.Key.Month, 1))
            .ToDictionary(month => month.Key, month => month.Sum(total => total.Value));
    }

    // The label of the bucket: the day name for a daily bucket, the short month name
    // for a monthly one.
    private static string BucketLabel(ChartBucket bucket) => bucket.Unit switch
    {
        ChartBucketUnit.Day => DayLabels[(int)bucket.Start.DayOfWeek],
        ChartBucketUnit.Month => MonthLabels[bucket.Start.Month - 1],
        _ => bucket.Start.ToString("yyyy-MM-dd")
    };

    private async Task<List<CategoryDataDto>> GetCategoryDataAsync(DateRange range, CancellationToken ct)
    {
        var categorySales = await context.SaleItems
            .Where(si => si.Sale.Status == SaleStatus.Completed
                         && si.Sale.BusinessDate >= range.From
                         && si.Sale.BusinessDate <= range.To)
            .GroupBy(si => si.ProductCategory)
            .Select(g => new { Category = g.Key ?? "Sin categoría", Total = g.Sum(si => si.Subtotal) })
            .ToListAsync(ct);

        var grandTotal = categorySales.Sum(c => c.Total);

        if (grandTotal == 0)
            return [];

        var colorIndex = 0;
        return categorySales
            .OrderByDescending(c => c.Total)
            .Select(c =>
            {
                var percent = (int)(c.Total / grandTotal * 100);
                var color = CategoryColors[colorIndex++ % CategoryColors.Length];
                return new CategoryDataDto(c.Category, percent, color, c.Total);
            })
            .ToList();
    }

    private async Task<List<TopProductDto>> GetTopProductsAsync(DateRange range, CancellationToken ct)
    {
        var topProducts = await context.SaleItems
            .Where(si => si.Sale.Status == SaleStatus.Completed
                         && si.Sale.BusinessDate >= range.From
                         && si.Sale.BusinessDate <= range.To)
            .GroupBy(si => new { si.ProductName, si.ProductCategory, si.ProductImageUrl })
            .Select(g => new
            {
                g.Key.ProductName,
                g.Key.ProductCategory,
                g.Key.ProductImageUrl,
                TotalQuantity = g.Sum(si => si.Quantity),
                TotalValue = g.Sum(si => si.Subtotal)
            })
            .OrderByDescending(p => p.TotalQuantity)
            .Take(5)
            .ToListAsync(ct);

        var rankIndex = 0;
        return topProducts
            .Select(p => new TopProductDto(
                p.ProductName,
                p.ProductCategory ?? "",
                p.TotalQuantity,
                p.TotalValue,
                p.ProductImageUrl ?? "",
                RankColors[rankIndex++ % RankColors.Length]
            ))
            .ToList();
    }

    private async Task<List<ActivityDto>> GetRecentActivitiesAsync(CancellationToken ct)
    {
        var recentSales = await context.Sales
            .OrderByDescending(s => s.CreatedAt)
            .Take(5)
            .Select(s => new
            {
                s.CreatedAt,
                Text = s.Status == SaleStatus.Completed
                    ? $"Venta completada: {s.ClienteName} - ${s.Total:N0}"
                    : $"Venta pendiente: {s.ClienteName} - ${s.Total:N0}",
                Type = s.Status == SaleStatus.Completed ? "sale" : "pending"
            })
            .ToListAsync(ct);

        var recentClients = await context.Clients
            .OrderByDescending(c => c.CreatedAt)
            .Take(3)
            .Select(c => new
            {
                CreatedAt = c.CreatedAt,
                Text = $"Nuevo cliente registrado: {c.Name}",
                Type = "client"
            })
            .ToListAsync(ct);

        var recentPurchases = await context.Purchases
            .Where(p => p.Status == EntityStatus.Completed)
            .OrderByDescending(p => p.CreatedAt)
            .Take(3)
            .Select(p => new
            {
                p.CreatedAt,
                Text = $"Compra recibida de {p.SupplierName} - ${p.Total:N0}",
                Type = "purchase"
            })
            .ToListAsync(ct);

        var allActivities = recentSales
            .Concat(recentClients)
            .Concat(recentPurchases)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(a => new ActivityDto(
                a.Text,
                a.CreatedAt.ToString("hh:mm tt"),
                a.Type
            ))
            .ToList();

        return allActivities;
    }

    private async Task<List<ReminderDto>> GetRemindersAsync(CancellationToken ct)
    {
        var pendingSalesCount = await context.Sales
            .CountAsync(s => s.Status == SaleStatus.Pending, ct);

        // The amount to collect is the real sum of the pending sales, not a count
        var pendingSalesTotal = await context.Sales
            .Where(s => s.Status == SaleStatus.Pending)
            .SumAsync(s => (decimal?)s.Total, ct) ?? 0;

        var lowStockCount = await context.Products
            .CountAsync(p => p.Stock <= 5 && p.IsActive, ct);

        var pendingPurchasesCount = await context.Purchases
            .CountAsync(p => p.Status == EntityStatus.Pending, ct);

        var reminders = new List<ReminderDto>();

        if (pendingSalesCount > 0)
            reminders.Add(new ReminderDto("Órdenes por despachar", pendingSalesCount, "#FEF3C7"));

        if (lowStockCount > 0)
            reminders.Add(new ReminderDto("Productos con stock bajo", lowStockCount, "#FEE2E2"));

        if (pendingPurchasesCount > 0)
            reminders.Add(new ReminderDto("Compras pendientes por recibir", pendingPurchasesCount, "#FEF3C7"));

        // Always show invoices pending
        reminders.Add(new ReminderDto("Facturas por cobrar", pendingSalesTotal, "#FEF3C7", true));

        return reminders;
    }

    private async Task<DashboardStatsDto> GetDashboardStatsAsync(DateRange range, CancellationToken ct)
    {
        var ordersCompleted = await context.Sales
            .CountAsync(s => s.Status == SaleStatus.Completed
                             && s.BusinessDate >= range.From
                             && s.BusinessDate <= range.To, ct);

        var ordersPending = await context.Sales
            .CountAsync(s => s.Status == SaleStatus.Pending, ct);

        var newProducts = await context.Products
            .CountAsync(p => p.BusinessDate >= range.From && p.BusinessDate <= range.To, ct);

        var activeSuppliers = await context.Suppliers
            .CountAsync(s => s.IsActive, ct);

        return new DashboardStatsDto(
            OrdersCompleted: ordersCompleted,
            OrdersPending: ordersPending,
            NewProducts: newProducts,
            ActiveSuppliers: activeSuppliers
        );
    }
}
