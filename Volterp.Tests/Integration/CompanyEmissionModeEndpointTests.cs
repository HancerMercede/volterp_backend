using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// The administrator of a company reads and changes the fiscal path of its
// future sales. The company always comes from the token.
[Collection("VolterpApiFactory")]
public class CompanyEmissionModeEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;

    public CompanyEmissionModeEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.EnsureSchemaCreated();

        lock (SeedLock)
        {
            if (!_seeded)
            {
                SeedAsync().GetAwaiter().GetResult();
                _seeded = true;
            }
        }
    }

    [Fact]
    public async Task TheAdministratorReadsTheModeOfTheCompanyOfTheToken()
    {
        await ResetModeAsync();
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync("/api/CompanyEmissionMode");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("emissionMode").GetString().Should().Be("Electronic");
    }

    [Fact]
    public async Task TheAdministratorChangesTheModeOfTheCompanyOfTheToken()
    {
        await ResetModeAsync();
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.PutAsJsonAsync(
            "/api/CompanyEmissionMode",
            new { emissionMode = "Normal", companyId = Guid7TestValues.CompanyB });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("emissionMode").GetString().Should().Be("Normal");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
        (await db.Companies.SingleAsync(c => c.Id == Guid7TestValues.CompanyA))
            .EmissionMode.Should().Be(CompanyEmissionMode.Normal);
        (await db.Companies.SingleAsync(c => c.Id == Guid7TestValues.CompanyB))
            .EmissionMode.Should().Be(CompanyEmissionMode.Electronic,
                "the company in the body cannot move the setting of another tenant");
    }

    [Fact]
    public async Task ANonAdminCannotChangeTheMode()
    {
        await ResetModeAsync();
        SetAuth(Guid7TestValues.CompanyA, "Employee");

        var response = await _client.PutAsJsonAsync(
            "/api/CompanyEmissionMode",
            new { emissionMode = "Normal" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangingTheMode_DoesNotTouchTheDocumentsAlreadyIssued()
    {
        await ResetModeAsync();
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.PutAsJsonAsync(
            "/api/CompanyEmissionMode",
            new { emissionMode = "Normal" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
        var invoice = await db.ElectronicInvoices
            .IgnoreQueryFilters()
            .SingleAsync(i => i.ENCF == "E320000000001");

        invoice.Status.Should().Be(EcfStatus.Accepted);
        invoice.SignedXml.Should().Be("<ECF />");
    }

    [Fact]
    public async Task AnUnknownModeIsRejected()
    {
        await ResetModeAsync();
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.PutAsJsonAsync(
            "/api/CompanyEmissionMode",
            new { emissionMode = 99 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task ResetModeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
        var company = await db.Companies.SingleAsync(c => c.Id == Guid7TestValues.CompanyA);
        company.EmissionMode = CompanyEmissionMode.Electronic;
        await db.SaveChangesAsync();
    }

    private void SetAuth(Guid companyId, string role = "Admin")
    {
        var token = JwtTestHelper.MintToken(_factory, companyId, Guid7TestValues.UserA, role);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        db.Companies.AddRange(
            new Company
            {
                Id = Guid7TestValues.CompanyA,
                Name = "Company A",
                TaxId = "130862346",
                Address = "1 A Street",
                LegalName = "Company A Legal",
                Email = "a@company.test",
                Phone = "555-0001",
                IsActive = true
            },
            new Company
            {
                Id = Guid7TestValues.CompanyB,
                Name = "Company B",
                TaxId = "130862354",
                Address = "2 B Street",
                LegalName = "Company B Legal",
                Email = "b@company.test",
                Phone = "555-0002",
                IsActive = true
            });

        db.Sales.Add(new Sale
        {
            Id = Guid7TestValues.SaleA,
            CompanyId = Guid7TestValues.CompanyA,
            Code = "V-0001",
            Status = SaleStatus.Completed,
            DocumentType = EcfDocumentType.Consumer,
            Subtotal = 100m,
            Itbis = 18m,
            Total = 118m
        });

        db.ElectronicInvoices.Add(new ElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = Guid7TestValues.CompanyA,
            SaleId = Guid7TestValues.SaleA,
            ENCF = "E320000000001",
            DocumentType = EcfDocumentType.Consumer,
            IssuerTaxId = "130862346",
            SignedXml = "<ECF />",
            Status = EcfStatus.Accepted
        });

        await db.SaveChangesAsync();
    }

}
