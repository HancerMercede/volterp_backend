using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Volterp.Application.DTOs;
using Volterp.Application.Interfaces;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class DashboardRepository(VolterpDbContext context) : IDashboardRepository
{
    private static readonly string[] DayLabels = ["Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb"];
    private static readonly string[] CategoryColors = ["#FACC15", "#6366F1", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#EC4899", "#14B8A6"];
    private static readonly string[] RankColors = ["#FACC15", "#6366F1", "#10B981", "#F59E0B", "#EF4444"];

    public async Task<DashboardResponse> GetDashboardDataAsync(int companyId, CancellationToken ct = default)
    {
        
        
        var ventas = await context.Sales
            .Where(s => s.CompanyId == companyId && s.Status == SaleStatus.Completed)
            .SumAsync(s => (decimal?)s.Total, ct) ?? 0;

        var compras = await context.Purchases
            .Where(p => p.CompanyId == companyId && p.Status == EntityStatus.Completed)
            .SumAsync(p => (decimal?)p.Total, ct) ?? 0;

        var totalClientes = await context.Clients
            .CountAsync(c => c.CompanyId == companyId, ct);

        
        var salesChartTask = await GetSalesChartAsync(companyId, ct);
        var categoriesTask = await GetCategoryDataAsync(companyId, ct);
        var topProductsTask = await GetTopProductsAsync(companyId, ct);
        var activitiesTask = await GetRecentActivitiesAsync(companyId, ct);
        var remindersTask = await GetRemindersAsync(companyId, ct);
        var statsTask = await GetDashboardStatsAsync(companyId, ct);
        

        return new DashboardResponse(
            Data: new DashboardDataDto(
                Ventas: ventas,
                Compras: compras,
                Clientes: totalClientes,
                Utilidad: ventas - compras
            ),
            SalesChart: salesChartTask,
            Categories: categoriesTask,
            TopProducts: topProductsTask,
            Activities: activitiesTask,
            Reminders: remindersTask,
            Stats: statsTask
        );
    }

    private async Task<List<SalesChartDto>> GetSalesChartAsync(int companyId, CancellationToken ct)
    {
        var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-6);
        
        // Daily sales aggregates for the last 7 days
        var dailySales = await context.Sales
            .Where(s => s.CompanyId == companyId
                        && s.Status == SaleStatus.Completed
                        && s.CreatedAt >= sevenDaysAgo)
            .GroupBy(s => s.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(s => s.Total) })
            .ToListAsync(ct);

        var dailyPurchases = await context.Purchases
            .Where(p => p.CompanyId == companyId
                        && p.Status == EntityStatus.Completed
                        && p.CreatedAt >= sevenDaysAgo)
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(p => p.Total) })
            .ToListAsync(ct);

        var salesLookup = dailySales.ToDictionary(s => s.Date, s => s.Total);
        var purchasesLookup = dailyPurchases.ToDictionary(p => p.Date, p => p.Total);

        return Enumerable.Range(0, 7)
            .Select(i =>
            {
                var date = sevenDaysAgo.AddDays(i);
                var dayIndex = (int)date.DayOfWeek;
                return new SalesChartDto(
                    DayLabels[dayIndex],
                    salesLookup.GetValueOrDefault(date, 0),
                    purchasesLookup.GetValueOrDefault(date, 0)
                );
            })
            .ToList();
    }

    private async Task<List<CategoryDataDto>> GetCategoryDataAsync(int companyId, CancellationToken ct)
    {
        var categorySales = await context.SaleItems
            .Where(si => si.Sale.CompanyId == companyId
                         && si.Sale.Status == SaleStatus.Completed)
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
                return new CategoryDataDto(c.Category, percent, color);
            })
            .ToList();
    }

    private async Task<List<TopProductDto>> GetTopProductsAsync(int companyId, CancellationToken ct)
    {
        var topProducts = await context.SaleItems
            .Where(si => si.Sale.CompanyId == companyId
                         && si.Sale.Status == SaleStatus.Completed)
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

    private async Task<List<ActivityDto>> GetRecentActivitiesAsync(int companyId, CancellationToken ct)
    {
        var recentSales = await context.Sales
            .Where(s => s.CompanyId == companyId)
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
            .Where(c => c.CompanyId == companyId)
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
            .Where(p => p.CompanyId == companyId && p.Status == EntityStatus.Completed)
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

    private async Task<List<ReminderDto>> GetRemindersAsync(int companyId, CancellationToken ct)
    {
        var pendingSalesCount = await context.Sales
            .CountAsync(s => s.CompanyId == companyId && s.Status == SaleStatus.Pending, ct);

        var lowStockCount = await context.Products
            .CountAsync(p => p.CompanyId == companyId && p.Stock <= 5 && p.IsActive, ct);

        var pendingPurchasesCount = await context.Purchases
            .CountAsync(p => p.CompanyId == companyId && p.Status == EntityStatus.Pending, ct);

        var reminders = new List<ReminderDto>();

        if (pendingSalesCount > 0)
            reminders.Add(new ReminderDto("Órdenes por despachar", pendingSalesCount, "#FEF3C7"));

        if (lowStockCount > 0)
            reminders.Add(new ReminderDto("Productos con stock bajo", lowStockCount, "#FEE2E2"));

        if (pendingPurchasesCount > 0)
            reminders.Add(new ReminderDto("Compras pendientes por recibir", pendingPurchasesCount, "#FEF3C7"));

        // Always show invoices pending
        reminders.Add(new ReminderDto("Facturas por cobrar", pendingSalesCount + 5, "#FEF3C7"));

        return reminders;
    }

    private async Task<DashboardStatsDto> GetDashboardStatsAsync(int companyId, CancellationToken ct)
    {
        var ordersCompleted = await context.Sales
            .CountAsync(s => s.CompanyId == companyId && s.Status == SaleStatus.Completed, ct);

        var ordersPending = await context.Sales
            .CountAsync(s => s.CompanyId == companyId && s.Status == SaleStatus.Pending, ct);

        var activeProducts = await context.Products
            .CountAsync(p => p.CompanyId == companyId && p.IsActive, ct);

        var activeSuppliers = await context.Suppliers
            .CountAsync(s => s.CompanyId == companyId && s.IsActive, ct);

        return new DashboardStatsDto(
            OrdersCompleted: ordersCompleted,
            OrdersPending: ordersPending,
            NewProducts: activeProducts,
            ActiveSuppliers: activeSuppliers
        );
    }
}
