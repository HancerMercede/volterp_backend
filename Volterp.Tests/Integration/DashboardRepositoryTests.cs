using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Application.Abstractions;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.Time;
using Volterp.Domain.ValueObjects;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;
using Xunit;

namespace Volterp.Tests.Integration;

/// <summary>
/// The dashboard aggregates must respect the reporting window: the flow metrics
/// are scoped to it, the stock metrics are not, and the chart follows its own
/// window (feature dashboard-period-filters).
/// </summary>
public class DashboardRepositoryTests
{
    private static readonly string[] DayLabels = ["Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb"];
    private static readonly string[] MonthLabels = ["Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"];

    // ─────── Flow metrics: scoped to the window ───────

    [Fact]
    public async Task GetDashboardDataAsync_CountsTheCompletedSalesInsideTheWindow_AndIgnoresTheOnesOutsideIt()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.Today, SaleStatus.Completed, 1, 100m);
        fixture.SeedSale(fixture.PreviousMonth, SaleStatus.Completed, 1, 500m);
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);

        response.Data.Ventas.Should().Be(100m,
            "a sale dated before the first day of the month is outside the window.");
    }

    [Fact]
    public async Task GetDashboardDataAsync_IgnoresTheSalesThatAreNotCompleted()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.Today, SaleStatus.Completed, 1, 100m);
        fixture.SeedSale(fixture.Today, SaleStatus.Pending, 1, 500m);
        fixture.SeedSale(fixture.Today, SaleStatus.Cancelled, 1, 900m);
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Today, ChartWindow.Week);

        response.Data.Ventas.Should().Be(100m, "only completed sales count as sales.");
    }

    [Fact]
    public async Task GetDashboardDataAsync_CountsTheCompletedPurchasesInsideTheWindow()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedPurchase(fixture.Today, EntityStatus.Completed, 300m);
        fixture.SeedPurchase(fixture.Today, EntityStatus.Pending, 900m);
        fixture.SeedPurchase(fixture.PreviousMonth, EntityStatus.Completed, 700m);
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);

        response.Data.Compras.Should().Be(300m,
            "only completed purchases dated inside the window count as purchases.");
    }

    [Fact]
    public async Task GetDashboardDataAsync_ReportsTheUtilityAsTheWindowSalesMinusTheWindowPurchases()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.Today, SaleStatus.Completed, 1, 1000m);
        fixture.SeedPurchase(fixture.Today, EntityStatus.Completed, 400m);
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Today, ChartWindow.Week);

        response.Data.Utilidad.Should().Be(600m);
    }

    [Fact]
    public async Task GetDashboardDataAsync_CountsOnlyTheClientsRegisteredInsideTheWindow()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedClient(fixture.Today);
        fixture.SeedClient(fixture.FirstOfMonth);
        fixture.SeedClient(fixture.PreviousMonth);
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);

        response.Data.Clientes.Should().Be(2,
            "'clientes' counts the clients registered inside the window, not every client ever created.");
    }

    [Fact]
    public async Task GetDashboardDataAsync_CountsTheCompletedOrdersInsideTheWindow()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.Today, SaleStatus.Completed, 1, 100m);
        fixture.SeedSale(fixture.PreviousMonth, SaleStatus.Completed, 1, 500m);
        fixture.SeedSale(fixture.Today, SaleStatus.Pending, 1, 700m);
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);

        response.Stats.OrdersCompleted.Should().Be(1,
            "'pedidos realizados' counts the completed orders of the window.");
    }

    [Fact]
    public async Task GetDashboardDataAsync_CountsOnlyTheProductsCreatedInsideTheWindow()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedProduct(fixture.Today, stock: 40);
        fixture.SeedProduct(fixture.PreviousMonth, stock: 40);
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);

        response.Stats.NewProducts.Should().Be(1,
            "'productos nuevos' counts the products created inside the window.");
    }

    [Fact]
    public async Task GetDashboardDataAsync_ScopesTheCategoriesToTheWindow()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.Today, SaleStatus.Completed, 1, 300m, category: "Bebidas");
        fixture.SeedSale(fixture.PreviousMonth, SaleStatus.Completed, 1, 700m, category: "Bebidas");
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);

        response.Categories.Should().ContainSingle();
        response.Categories[0].Name.Should().Be("Bebidas");
        response.Categories[0].Total.Should().Be(300m,
            "the category amounts come from the sales of the window only.");
        response.Categories[0].Percent.Should().Be(100);
    }

    [Fact]
    public async Task GetDashboardDataAsync_ScopesTheTopProductsToTheWindow()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.Today, SaleStatus.Completed, 5, 100m, productName: "Café", category: "Bebidas");
        fixture.SeedSale(fixture.PreviousMonth, SaleStatus.Completed, 9, 100m, productName: "Café", category: "Bebidas");
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);

        response.TopProducts.Should().ContainSingle();
        response.TopProducts[0].Name.Should().Be("Café");
        response.TopProducts[0].Sales.Should().Be(5, "only the sales of the window count.");
        response.TopProducts[0].Value.Should().Be(500m);
    }

    // ─────── Change percentages ───────

    [Fact]
    public async Task GetDashboardDataAsync_ReportsAPositiveChangePercent_WhenTheCurrentPeriodBeatsThePreviousOne()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.Today, SaleStatus.Completed, 1, 150m);
        fixture.SeedSale(fixture.PreviousPeriodStart, SaleStatus.Completed, 1, 100m);
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);

        response.Data.VentasChangePercent.Should().Be(50m);
    }

    [Fact]
    public async Task GetDashboardDataAsync_ReportsANegativeChangePercent_WhenTheCurrentPeriodIsBelowThePreviousOne()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.Today, SaleStatus.Completed, 1, 50m);
        fixture.SeedSale(fixture.PreviousPeriodStart, SaleStatus.Completed, 1, 100m);
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);

        response.Data.VentasChangePercent.Should().Be(-50m);
    }

    [Fact]
    public async Task GetDashboardDataAsync_ReportsANullChangePercent_WhenThePreviousPeriodIsEmpty()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.Today, SaleStatus.Completed, 1, 150m);
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);

        response.Data.VentasChangePercent.Should().BeNull(
            "the comparison is undefined when the previous period had no value to compare against; " +
            "it must never be coerced to 0 or to 100.");
    }

    [Fact]
    public async Task GetDashboardDataAsync_ReportsTheChangePercent_OfPurchasesAndClients()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedPurchase(fixture.Today, EntityStatus.Completed, 100m);
        fixture.SeedPurchase(fixture.PreviousPeriodStart, EntityStatus.Completed, 50m);
        fixture.SeedClient(fixture.Today);
        fixture.SeedClient(fixture.Today);
        fixture.SeedClient(fixture.PreviousPeriodStart);
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);

        response.Data.ComprasChangePercent.Should().Be(100m);
        response.Data.ClientesChangePercent.Should().Be(100m);
    }

    // ─────── Stock metrics: point in time, never window-scoped ───────

    [Fact]
    public async Task GetDashboardDataAsync_DoesNotScopeTheStockMetricsToTheWindow()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.PreviousMonth, SaleStatus.Pending, 1, 100m);
        fixture.SeedSupplier(isActive: true);
        fixture.SeedSupplier(isActive: false);
        fixture.SeedProduct(fixture.PreviousMonth, stock: 2);
        await fixture.SaveAsync();

        var today = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Today, ChartWindow.Week);
        var month = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);

        today.Stats.OrdersPending.Should().Be(1, "pending orders describe the current state of the business.");
        month.Stats.OrdersPending.Should().Be(1);
        today.Stats.ActiveSuppliers.Should().Be(1);
        month.Stats.ActiveSuppliers.Should().Be(1);
        today.Reminders.Should().Contain(r => r.Text == "Productos con stock bajo" && r.Count == 1,
            "the low stock reminder is a point-in-time fact, not a window aggregate.");
    }

    [Fact]
    public async Task GetDashboardDataAsync_ReportsTheRealPendingAmountInTheReceivablesReminder()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.Today, SaleStatus.Pending, 1, 120m);
        fixture.SeedSale(fixture.Today, SaleStatus.Pending, 1, 130m);
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Today, ChartWindow.Week);

        var reminder = response.Reminders.Should().ContainSingle(r => r.Text == "Facturas por cobrar").Subject;
        reminder.Count.Should().Be(250m,
            "the amount to collect is the real sum of the pending sales, not the pending count plus five.");
    }

    // ─────── The chart ───────

    [Fact]
    public async Task GetDashboardDataAsync_ReturnsOneChartEntryPerBucketOfTheWindow_IncludingTheEmptyOnes()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.Today, SaleStatus.Completed, 1, 100m);
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Month);

        response.SalesChart.Should().HaveCount(
            ReportingPeriod.Buckets(ChartWindow.Month, fixture.Today).Count,
            "the month window holds one bucket per day, from the first of the month up to today.");

        response.SalesChart.Should().ContainSingle(b => b.Sales == 100m,
            "the sale lands in its own bucket.");
        response.SalesChart.Count(b => b.Sales == 0).Should().Be(response.SalesChart.Count - 1,
            "a bucket with no data is present with a zero; a gap in the series would be a lie.");
    }

    [Fact]
    public async Task GetDashboardDataAsync_ChartFollowsTheChartWindow_AndIgnoresTheGlobalScope()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.Today, SaleStatus.Completed, 1, 100m);
        await fixture.SaveAsync();

        var weeklyWithMonthScope = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);
        var weeklyWithTodayScope = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Today, ChartWindow.Week);
        var monthly = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Month);
        var yearly = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Year);

        weeklyWithMonthScope.SalesChart.Should().HaveCount(
            ReportingPeriod.Buckets(ChartWindow.Week, fixture.Today).Count,
            "the week window holds one bucket per day, from Monday up to today.");
        weeklyWithTodayScope.SalesChart.Should().HaveCount(
            ReportingPeriod.Buckets(ChartWindow.Week, fixture.Today).Count);
        monthly.SalesChart.Should().HaveCount(
            ReportingPeriod.Buckets(ChartWindow.Month, fixture.Today).Count);
        yearly.SalesChart.Should().HaveCount(
            ReportingPeriod.Buckets(ChartWindow.Year, fixture.Today).Count,
            "the year window holds one bucket per month.");

        weeklyWithTodayScope.SalesChart.Should().BeEquivalentTo(weeklyWithMonthScope.SalesChart,
            "the chart follows the chart window only; the global scope does not change the series.");
    }

    [Fact]
    public async Task GetDashboardDataAsync_LabelsADailyBucketWithTheDayName_AndAMonthlyBucketWithTheMonthName()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.Today, SaleStatus.Completed, 1, 100m);
        await fixture.SaveAsync();

        var weekly = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);
        var yearly = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Year);

        var expectedDayLabels = ReportingPeriod
            .Buckets(ChartWindow.Week, fixture.Today)
            .Select(b => DayLabels[(int)b.Start.DayOfWeek]);

        weekly.SalesChart.Select(b => b.Day).Should().Equal(expectedDayLabels);

        var expectedMonthLabels = ReportingPeriod
            .Buckets(ChartWindow.Year, fixture.Today)
            .Select(b => MonthLabels[b.Start.Month - 1]);

        yearly.SalesChart.Select(b => b.Day).Should().Equal(expectedMonthLabels);
    }

    [Fact]
    public async Task GetDashboardDataAsync_GroupsTheYearlyChartByMonth()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.Today, SaleStatus.Completed, 1, 100m);
        fixture.SeedSale(fixture.FirstOfMonth, SaleStatus.Completed, 1, 50m);
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Year);

        var currentMonth = response.SalesChart.Single(b => b.Day == MonthLabels[fixture.Today.Month - 1]);
        currentMonth.Sales.Should().Be(150m, "both sales of the current month land in the same monthly bucket.");
    }

    // ─────── Isolation ───────

    [Fact]
    public async Task GetDashboardDataAsync_NeverCrossesTenants()
    {
        await using var fixture = new DashboardFixture();
        await fixture.InitializeAsync();

        fixture.SeedSale(fixture.Today, SaleStatus.Completed, 1, 100m, category: "Bebidas");
        fixture.SeedSale(fixture.Today, SaleStatus.Completed, 1, 9000m, category: "Bebidas", companyId: DashboardFixture.OtherCompanyId);
        fixture.SeedPurchase(fixture.Today, EntityStatus.Completed, 400m);
        fixture.SeedPurchase(fixture.Today, EntityStatus.Completed, 8000m, companyId: DashboardFixture.OtherCompanyId);
        fixture.SeedClient(fixture.Today);
        fixture.SeedClient(fixture.Today, companyId: DashboardFixture.OtherCompanyId);
        await fixture.SaveAsync();

        var response = await fixture.Repository.GetDashboardDataAsync(DashboardScope.Today, ChartWindow.Week);

        response.Data.Ventas.Should().Be(100m, "another tenant's sales must never be aggregated.");
        response.Data.Compras.Should().Be(400m);
        response.Data.Clientes.Should().Be(1);
        response.Categories.Should().ContainSingle("the categories reach the sale through its items; the join must not cross tenants either.");
        response.Categories[0].Total.Should().Be(100m);
        response.TopProducts.Should().ContainSingle();
        response.TopProducts[0].Sales.Should().Be(1);
        response.TopProducts[0].Value.Should().Be(100m);
    }

    // ─────── Fixture ───────

    private sealed class DashboardFixture : IAsyncDisposable
    {
        public static readonly Guid CompanyId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d01");
        public static readonly Guid OtherCompanyId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d02");

        private readonly SqliteConnection _connection;
        private readonly VolterpDbContext _context;

        public DashboardFixture()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new VolterpDbContext(options, new FixedTenantContext(CompanyId));

            // The repository reads the business date from the system clock; the
            // fixture anchors its dates on the same business day.
            Today = BusinessCalendar.Today(DateTime.UtcNow);
            FirstOfMonth = ReportingPeriod.StartOfMonth(Today);
            PreviousMonth = Today.AddMonths(-1);
            PreviousPeriodStart = ReportingPeriod.Previous(DashboardScope.Month, Today).From;
        }

        public DateOnly Today { get; }
        public DateOnly FirstOfMonth { get; }
        public DateOnly PreviousMonth { get; }
        public DateOnly PreviousPeriodStart { get; }

        public DashboardRepository Repository { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await _context.Database.EnsureCreatedAsync();

            _context.Companies.AddRange(
                new Company
                {
                    Id = CompanyId,
                    Name = "Empresa A",
                    TaxId = "130862346",
                    Address = "1 A Street",
                    LegalName = "Empresa A Legal",
                    Phone = "8090000000",
                    Email = "a@company.test"
                },
                new Company
                {
                    Id = OtherCompanyId,
                    Name = "Empresa B",
                    TaxId = "130862354",
                    Address = "2 B Street",
                    LegalName = "Empresa B Legal",
                    Phone = "8090000001",
                    Email = "b@company.test"
                });

            await _context.SaveChangesAsync();

            Repository = new DashboardRepository(_context);
        }

        public Task SaveAsync() => _context.SaveChangesAsync();

        public void SeedSale(DateOnly businessDate, SaleStatus status, int quantity, decimal unitPrice,
            string productName = "Producto", string? category = null, Guid? companyId = null)
        {
            var sale = new Sale
            {
                Id = Guid.CreateVersion7(),
                CompanyId = companyId ?? CompanyId,
                Code = $"V-{Guid.CreateVersion7():N}",
                BusinessDate = businessDate,
                Status = status,
                ClienteName = "Cliente de prueba"
            };

            // Exento pays no ITBIS, so the sale total is exactly quantity * unitPrice.
            sale.AddItem(Guid.CreateVersion7(), productName, quantity, unitPrice, TaxCode.Exento);

            if (category is not null)
                sale.Items.First().ProductCategory = category;

            _context.Sales.Add(sale);
        }

        public void SeedPurchase(DateOnly businessDate, EntityStatus status, decimal total, Guid? companyId = null)
        {
            var purchase = new Purchase
            {
                Id = Guid.CreateVersion7(),
                CompanyId = companyId ?? CompanyId,
                Code = $"C-{Guid.CreateVersion7():N}",
                BusinessDate = businessDate,
                Status = status,
                SupplierName = "Proveedor"
            };

            purchase.AddItem(null, "Producto", 1, total);

            _context.Purchases.Add(purchase);
        }

        public void SeedClient(DateOnly businessDate, Guid? companyId = null)
        {
            var id = Guid.CreateVersion7();

            _context.Clients.Add(new Client
            {
                Id = id,
                CompanyId = companyId ?? CompanyId,
                Code = $"C-{id:N}",
                Name = "Cliente",
                Email = $"{id:N}@test.local",
                Phone = "8090000000",
                Address = "Dirección",
                BusinessDate = businessDate
            });
        }

        public void SeedProduct(DateOnly businessDate, int stock, bool isActive = true, Guid? companyId = null)
        {
            var id = Guid.CreateVersion7();

            _context.Products.Add(new Product
            {
                Id = id,
                CompanyId = companyId ?? CompanyId,
                Code = $"P-{id:N}",
                Name = "Producto",
                Category = "Categoría",
                Stock = stock,
                IsActive = isActive,
                BusinessDate = businessDate,
                Price = new Money(100m, Currency.DOP)
            });
        }

        public void SeedSupplier(bool isActive, Guid? companyId = null)
        {
            var id = Guid.CreateVersion7();

            _context.Suppliers.Add(new Supplier
            {
                Id = id,
                CompanyId = companyId ?? CompanyId,
                Code = $"S-{id:N}",
                Name = "Proveedor",
                Email = $"{id:N}@test.local",
                Phone = "8090000000",
                Address = "Dirección",
                Category = "Mayorista",
                ContactPerson = "Contacto",
                IsActive = isActive
            });
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FixedTenantContext(Guid companyId) : ITenantContext
    {
        public Guid? CompanyId => companyId;

        public bool IsUnrestricted => false;
    }
}
