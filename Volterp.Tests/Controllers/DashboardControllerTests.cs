using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Application.Abstractions;
using Volterp.Application.DTOs;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.Time;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Tests.Integration;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Controllers;

/// <summary>
/// The dashboard endpoint contract: two optional query parameters, one for the
/// global scope and one for the chart window (feature dashboard-period-filters).
/// </summary>
[Collection("VolterpApiFactory")]
public class DashboardControllerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DashboardControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureSchemaCreated();
    }

    // ─────── Validation: an unknown value must never fall back to a default ───────

    [Fact]
    public async Task GetDashboard_WithAnUnknownPeriod_ReturnsBadRequest()
    {
        // ARRANGE
        var client = await CreateAuthenticatedClientAsync();

        // ACT
        var response = await client.GetAsync("/api/dashboard?period=quarter");

        // ASSERT
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "an unknown period must be rejected instead of silently falling back to a default.");

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().Contain("today")
            .And.Contain("week")
            .And.Contain("month",
            "the message must name the accepted values so the client can correct the request.");
    }

    [Fact]
    public async Task GetDashboard_WithAnUnknownChartPeriod_ReturnsBadRequest()
    {
        // ARRANGE
        var client = await CreateAuthenticatedClientAsync();

        // ACT
        var response = await client.GetAsync("/api/dashboard?chartPeriod=decade");

        // ASSERT
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "an unknown chart window must be rejected instead of silently falling back to a default.");

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().Contain("week")
            .And.Contain("month")
            .And.Contain("year",
            "the message must name the accepted values so the client can correct the request.");
    }

    // ─────── Defaults: period = month, chartPeriod = week ───────

    [Fact]
    public async Task GetDashboard_WithoutQueryParameters_ScopesTheFlowMetricsToTheMonth_AndTheChartToTheWeek()
    {
        // ARRANGE
        var today = BusinessCalendar.Today(DateTime.UtcNow);
        var client = await CreateAuthenticatedClientAsync((db, companyId) =>
        {
            db.Sales.Add(BuildSale(today, SaleStatus.Completed, 100m, "V-DEFAULT-IN", companyId));
            db.Sales.Add(BuildSale(today.AddMonths(-1), SaleStatus.Completed, 500m, "V-DEFAULT-OUT", companyId));
        });

        // ACT
        var response = await client.GetAsync("/api/dashboard");

        // ASSERT
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>();

        body.Should().NotBeNull();
        body!.Data.Ventas.Should().Be(100m,
            "the default period is 'month': only the current calendar month counts, " +
            "a sale from the previous month is outside the window.");
        body.SalesChart.Should().HaveCount(
            ReportingPeriod.Buckets(ChartWindow.Week, today).Count,
            "the default chart window is 'week', so the series holds one point per day of this week.");
    }

    [Fact]
    public async Task GetDashboard_WithAnExplicitPeriod_ScopesTheFlowMetricsToThatPeriod()
    {
        // ARRANGE
        var today = BusinessCalendar.Today(DateTime.UtcNow);
        var client = await CreateAuthenticatedClientAsync((db, companyId) =>
        {
            db.Sales.Add(BuildSale(today, SaleStatus.Completed, 100m, "V-TODAY-IN", companyId));
            db.Sales.Add(BuildSale(today.AddDays(-1), SaleStatus.Completed, 500m, "V-TODAY-OUT", companyId));
        });

        // ACT
        var response = await client.GetAsync("/api/dashboard?period=today");

        // ASSERT
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>();

        body.Should().NotBeNull();
        body!.Data.Ventas.Should().Be(100m,
            "'today' scopes the flow metrics to the current business day only.");
    }

    // ─────── The two filters are independent (S4) ───────

    [Fact]
    public async Task GetDashboard_ChangingTheChartWindow_DoesNotChangeTheFlowMetrics()
    {
        // ARRANGE
        var today = BusinessCalendar.Today(DateTime.UtcNow);
        var client = await CreateAuthenticatedClientAsync((db, companyId) =>
        {
            db.Sales.Add(BuildSale(today, SaleStatus.Completed, 100m, "V-SCOPE-IN", companyId));
            db.Sales.Add(BuildSale(today.AddMonths(-1), SaleStatus.Completed, 500m, "V-SCOPE-OUT", companyId));
        });

        // ACT
        var week = await client.GetAsync("/api/dashboard?period=month&chartPeriod=week");
        var year = await client.GetAsync("/api/dashboard?period=month&chartPeriod=year");

        // ASSERT
        week.StatusCode.Should().Be(HttpStatusCode.OK);
        year.StatusCode.Should().Be(HttpStatusCode.OK);

        var weekBody = await week.Content.ReadFromJsonAsync<DashboardResponse>();
        var yearBody = await year.Content.ReadFromJsonAsync<DashboardResponse>();

        yearBody!.Data.Ventas.Should().Be(weekBody!.Data.Ventas,
            "the chart window applies to the chart only; the KPIs keep the global scope.");
        yearBody.Data.Clientes.Should().Be(weekBody.Data.Clientes);
        yearBody.Stats.Should().BeEquivalentTo(weekBody.Stats);

        yearBody.SalesChart.Should().HaveCount(
            ReportingPeriod.Buckets(ChartWindow.Year, today).Count,
            "a yearly chart window holds one point per month.");
        weekBody.SalesChart.Should().HaveCount(
            ReportingPeriod.Buckets(ChartWindow.Week, today).Count,
            "a weekly chart window holds one point per day.");
    }

    // ─────── Case insensitivity ───────

    [Fact]
    public async Task GetDashboard_AcceptsTheValidValuesCaseInsensitively()
    {
        // ARRANGE
        var today = BusinessCalendar.Today(DateTime.UtcNow);
        var client = await CreateAuthenticatedClientAsync((db, companyId) =>
        {
            db.Sales.Add(BuildSale(today, SaleStatus.Completed, 100m, "V-CASE-IN", companyId));
            db.Sales.Add(BuildSale(today.AddDays(-1), SaleStatus.Completed, 500m, "V-CASE-OUT", companyId));
        });

        // ACT
        var response = await client.GetAsync("/api/dashboard?period=TODAY&chartPeriod=WeEk");

        // ASSERT
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the accepted values are matched case-insensitively.");
        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>();

        body.Should().NotBeNull();
        body!.Data.Ventas.Should().Be(100m, "'TODAY' means the same as 'today'.");
    }

    // ─────── Compatibility: the endpoint keeps working without parameters (S9) ───────

    [Fact]
    public async Task GetDashboard_WithoutQueryParameters_StillReturnsTheDashboard()
    {
        // ARRANGE
        var client = await CreateAuthenticatedClientAsync();

        // ACT
        var response = await client.GetAsync("/api/dashboard");

        // ASSERT
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /api/dashboard with no query parameters must keep working.");
        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>();

        body.Should().NotBeNull();
        body!.Data.Should().NotBeNull();
        body.SalesChart.Should().NotBeNull();
        body.Stats.Should().NotBeNull();
    }

    // ─────── Helpers ───────

    private static Sale BuildSale(DateOnly businessDate, SaleStatus status, decimal total, string code, Guid companyId)
    {
        var sale = new Sale
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            Code = code,
            BusinessDate = businessDate,
            Status = status,
            ClienteName = "Cliente de prueba"
        };

        // Exento pays no ITBIS, so the line subtotal is exactly the sale total.
        sale.AddItem(Guid.CreateVersion7(), "Producto", 1, total, TaxCode.Exento);

        return sale;
    }

    /// <summary>
    /// Creates a tenant of its own so every test asserts against its own rows only,
    /// and returns a client carrying that tenant's token.
    /// </summary>
    private async Task<HttpClient> CreateAuthenticatedClientAsync(Action<VolterpDbContext, Guid>? seed = null)
    {
        var companyId = Guid.CreateVersion7();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
            var connection = (SqliteConnection)db.Database.GetDbConnection();
            var options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(connection)
                .Options;

            using var unrestricted = new VolterpDbContext(options, new NullTenantContext());

            unrestricted.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Empresa de prueba",
                TaxId = Guid.CreateVersion7().ToString("N")[..13],
                Address = "1 A Street",
                LegalName = "Empresa de prueba",
                Phone = "8090000000",
                Email = $"{companyId:N}@test.local"
            });

            seed?.Invoke(unrestricted, companyId);

            await unrestricted.SaveChangesAsync();
        }

        var token = JwtTestHelper.MintToken(_factory, companyId, Guid7TestValues.UserA);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}
