using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.CategoryDtos;
using Volterp.Application.DTOs.CompanyCertificateDtos;
using Volterp.Application.DTOs.ProductDtos;
using Volterp.Application.DTOs.SaleDtos;
using Volterp.Application.DTOs.SupplierDtos;
using Volterp.Application.DTOs.UserDtos;
using Xunit;

namespace Volterp.Tests.DTOs;

public class WireContractTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CreateSaleRequest_HasNoCompanyIdProperty()
    {
        AssertRequestDtoHasNoCompanyIdProperty(typeof(CreateSaleRequest));
    }

    [Fact]
    public void CreateCategoryRequest_HasNoCompanyIdProperty()
    {
        AssertRequestDtoHasNoCompanyIdProperty(typeof(CreateCategoryRequest));
    }

    [Fact]
    public void CreateProductDto_HasNoCompanyIdProperty()
    {
        AssertRequestDtoHasNoCompanyIdProperty(typeof(CreateProductDto));
    }

    [Fact]
    public void CreateUserDto_HasNoCompanyIdProperty()
    {
        AssertRequestDtoHasNoCompanyIdProperty(typeof(CreateUserDto));
    }

    [Fact]
    public void SupplierCreateDto_HasNoCompanyIdProperty()
    {
        AssertRequestDtoHasNoCompanyIdProperty(typeof(SupplierCreateDto));
    }

    [Fact]
    public void UpdateSupplierRequest_HasNoCompanyIdProperty()
    {
        AssertRequestDtoHasNoCompanyIdProperty(typeof(UpdateSupplierRequest));
    }

    [Fact]
    public void CreateCompanyCertificateDto_HasNoCompanyIdProperty()
    {
        AssertRequestDtoHasNoCompanyIdProperty(typeof(CreateCompanyCertificateDto));
    }

    // ─────── JSON-level contract: a "companyId" field in the body is silently dropped ───────

    [Fact]
    public void CreateSaleRequest_DeserializationIgnoresCompanyIdField_InBody()
    {
        // If a malicious client posts {"companyId": 999, ...} the value is
        // discarded because the property does not exist on CreateSaleRequest.
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var json = """
        {
          "companyId": 999,
          "clienteId": null,
          "clienteName": "Cross-tenant attempt",
          "status": 0,
          "total": 100,
          "notes": null,
          "items": []
        }
        """;

        // ACT
        var act = () => JsonSerializer.Deserialize<CreateSaleRequest>(json, opts);

        // ASSERT — deserialization must succeed; the unknown field is dropped.
        act.Should().NotThrow();
        var deserialized = JsonSerializer.Deserialize<CreateSaleRequest>(json, opts);
        deserialized.Should().NotBeNull();
    }

    [Fact]
    public void SupplierCreateDto_DeserializationIgnoresCompanyIdField_InBody()
    {
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var json = """
        {
          "companyId": 999,
          "name": "Cross-tenant attempt",
          "email": "x@example.com",
          "phone": "1",
          "address": "1",
          "category": "x",
          "contactPerson": "x",
          "isActive": true
        }
        """;

        var act = () => JsonSerializer.Deserialize<SupplierCreateDto>(json, opts);

        act.Should().NotThrow();
        var deserialized = JsonSerializer.Deserialize<SupplierCreateDto>(json, opts);
        deserialized.Should().NotBeNull();
    }

    [Fact]
    public void UpdateSupplierRequest_DeserializationIgnoresCompanyIdField_InBody()
    {
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var json = """
        {
          "id": "0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e01",
          "companyId": 999,
          "name": "Cross-tenant attempt",
          "email": "x@example.com",
          "phone": "1",
          "address": "1",
          "category": "x",
          "contactPerson": "x",
          "isActive": true
        }
        """;

        var act = () => JsonSerializer.Deserialize<UpdateSupplierRequest>(json, opts);

        act.Should().NotThrow();
        var deserialized = JsonSerializer.Deserialize<UpdateSupplierRequest>(json, opts);
        deserialized.Should().NotBeNull();
    }

    // ─────── JSON-level contract: the dashboard response shape (feature dashboard-period-filters) ───────

    [Fact]
    public void DashboardDataDto_SerializesTheChangePercentages()
    {
        // ARRANGE
        var dto = new DashboardDataDto(100m, 40m, 3, 60m, 10m, -5m, null, null);

        // ACT
        var json = JsonSerializer.Serialize(dto, WebJson);

        // ASSERT
        json.Should().Contain("ventasChangePercent")
            .And.Contain("comprasChangePercent")
            .And.Contain("clientesChangePercent")
            .And.Contain("utilidadChangePercent",
            "the client renders the change of every KPI; the values must travel in the response.");
    }

    [Fact]
    public void DashboardDataDto_SerializesAnUndefinedChangePercentAsNull()
    {
        // ARRANGE
        var dto = new DashboardDataDto(100m, 40m, 3, 60m, null, null, null, null);

        // ACT
        var json = JsonSerializer.Serialize(dto, WebJson);
        using var document = JsonDocument.Parse(json);

        // ASSERT
        document.RootElement.TryGetProperty("ventasChangePercent", out var element)
            .Should().BeTrue("the dashboard must publish the change percentage so the client can render it");
        element.ValueKind.Should().Be(JsonValueKind.Null,
            "a null change percentage means the comparison is undefined; it must never be coerced to 0 or 100.");
    }

    [Fact]
    public void CategoryDataDto_ExposesTheDonutTotal()
    {
        // ASSERT
        var property = typeof(CategoryDataDto).GetProperty("Total");

        property.Should().NotBeNull(
            "the donut total must come from the response instead of being hardcoded by the client.");
        property!.PropertyType.Should().Be(typeof(decimal),
            "the donut total is a money amount, not a percentage.");
    }

    [Fact]
    public void CategoryDataDto_SerializesTheDonutTotal()
    {
        // ARRANGE
        var dto = new CategoryDataDto("Bebidas", 40, "#FACC15", 145660m);

        // ACT
        var json = JsonSerializer.Serialize(dto, WebJson);
        using var document = JsonDocument.Parse(json);

        // ASSERT
        document.RootElement.TryGetProperty("total", out var element)
            .Should().BeTrue("the donut total must travel in the response.");
        element.GetDecimal().Should().Be(145660m);
    }

    [Fact]
    public void SalesChartDto_KeepsTheDayField_AndCarriesTheBucketLabel()
    {
        // ARRANGE
        var dto = new SalesChartDto("Ene", 10m, 5m);

        // ACT
        var json = JsonSerializer.Serialize(dto, WebJson);
        using var document = JsonDocument.Parse(json);

        // ASSERT
        document.RootElement.TryGetProperty("day", out var element)
            .Should().BeTrue(
                "the field is named 'day' for compatibility; renaming it would ripple through the DTO, " +
                "the client type, the mock and the chart for no behavioural gain.");
        element.GetString().Should().Be("Ene",
            "a monthly bucket carries a month name in the 'day' field.");
    }

    [Fact]
    public void ReminderDto_CarriesTheRealPendingAmount()
    {
        // ARRANGE
        var dto = new ReminderDto("Facturas por cobrar", 145660.50m, "#FEF3C7", true);

        // ACT
        var json = JsonSerializer.Serialize(dto, WebJson);
        using var document = JsonDocument.Parse(json);

        // ASSERT
        document.RootElement.GetProperty("count").GetDecimal().Should().Be(145660.50m,
            "the receivables reminder carries the real amount to collect, not a fabricated count.");
        document.RootElement.GetProperty("isAmount").GetBoolean().Should().BeTrue(
            "the client must be told the value is an amount, not a count of items.");
    }

    [Fact]
    public void ReminderDto_KeepsACountReminderMarkedAsACount()
    {
        // ARRANGE
        var dto = new ReminderDto("Ordenes por despachar", 12m, "#FEF3C7");

        // ACT
        var json = JsonSerializer.Serialize(dto, WebJson);
        using var document = JsonDocument.Parse(json);

        // ASSERT
        document.RootElement.GetProperty("isAmount").GetBoolean().Should().BeFalse(
            "a count of items is not an amount to collect.");
    }

    [Fact]
    public void DashboardResponse_SerializesTheWholeDashboardResponse()
    {
        // ARRANGE
        var response = new DashboardResponse(
            Data: new DashboardDataDto(100m, 40m, 3, 60m, 10m, -5m, null, null),
            SalesChart: [new SalesChartDto("Ene", 10m, 5m)],
            Categories: [new CategoryDataDto("Bebidas", 100, "#FACC15", 145660m)],
            TopProducts: [],
            Activities: [],
            Reminders: [new ReminderDto("Facturas por cobrar", 145660m, "#FEF3C7", true)],
            Stats: new DashboardStatsDto(1, 2, 3, 4));

        // ACT
        var json = JsonSerializer.Serialize(response, WebJson);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // ASSERT
        root.GetProperty("data").GetProperty("utilidadChangePercent").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("salesChart")[0].GetProperty("day").GetString().Should().Be("Ene");
        root.GetProperty("categories")[0].GetProperty("total").GetDecimal().Should().Be(145660m);
        root.GetProperty("reminders")[0].GetProperty("count").GetDecimal().Should().Be(145660m);
        root.GetProperty("reminders")[0].GetProperty("isAmount").GetBoolean().Should().BeTrue();
    }

    // ─────── Shared helper ───────

    private static void AssertRequestDtoHasNoCompanyIdProperty(Type t)
    {
        // Bind to instance — protects against accidental future static fields.
        var prop = t.GetProperty(
            "CompanyId",
            BindingFlags.Public | BindingFlags.Instance);

        prop.Should().BeNull(
            $"the request-body DTO '{t.Name}' MUST NOT expose CompanyId. " +
            "Tenant scope is decided exclusively by the JWT-derived companyId " +
            "the controller passes to the application service. Adding CompanyId " +
            "back here would re-open the cross-tenant bugs closed by the wire refactor.");
    }
}
