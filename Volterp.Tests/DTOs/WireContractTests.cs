using System.Reflection;
using System.Text.Json;
using FluentAssertions;
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
          "id": 5,
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
