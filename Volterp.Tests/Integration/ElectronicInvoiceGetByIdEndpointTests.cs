using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// The read of a single document: the company comes from the token and the
// document of another tenant is a 404.
[Collection("VolterpApiFactory")]
public class ElectronicInvoiceGetByIdEndpointTests : IClassFixture<ApiFactory>
{
    private const string Encf = "E320000000001";
    private const string OtherTenantEncf = "E320000000002";
    private const string QrUrl = "https://ecf.dgii.gov.do/verify?code=ABC";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;
    private static Guid _invoiceId;
    private static Guid _otherTenantInvoiceId;

    public ElectronicInvoiceGetByIdEndpointTests(ApiFactory factory)
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
    public async Task TheDocumentOfTheTenantIsAnsweredWithItsFiscalState()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoices/{_invoiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        root.GetProperty("id").GetGuid().Should().Be(_invoiceId);
        root.GetProperty("encf").GetString().Should().Be(Encf);
        root.GetProperty("saleId").GetGuid().Should().Be(Guid7TestValues.SaleA,
            "the front joins the document with the sale on the screen");
        root.GetProperty("documentType").GetString().Should().Be("Consumer");
        root.GetProperty("status").GetString().Should().Be("Accepted");
        root.GetProperty("total").GetDecimal().Should().Be(236m);
        root.GetProperty("qrUrl").GetString().Should().Be(QrUrl,
            "the front shows the verification of the document");
    }

    [Fact]
    public async Task TheDocumentOfAnotherTenantIsNotFound()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoices/{_otherTenantInvoiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheDocumentThatDoesNotExistIsNotFound()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoices/{Guid.CreateVersion7()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheStateOfTheTenantIsStillAnswered()
    {
        // The state and the document share the controller: the literal wins
        // over the id, so the list is not broken by the read of one document.
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync("/api/ElectronicInvoices/state");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private void SetAuth(Guid companyId)
    {
        var token = JwtTestHelper.MintToken(_factory, companyId, Guid7TestValues.UserA);

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
                Phone = "809-555-1234",
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
                Phone = "809-555-1235",
                IsActive = true
            });

        var otherTenantSaleId = Guid.CreateVersion7();

        db.Sales.AddRange(
            new Sale
            {
                Id = Guid7TestValues.SaleA,
                CompanyId = Guid7TestValues.CompanyA,
                Code = "V-0001",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            },
            new Sale
            {
                Id = otherTenantSaleId,
                CompanyId = Guid7TestValues.CompanyB,
                Code = "V-0002",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                Total = 100m
            });

        _invoiceId = Guid.CreateVersion7();
        _otherTenantInvoiceId = Guid.CreateVersion7();

        db.ElectronicInvoices.AddRange(
            new ElectronicInvoice
            {
                Id = _invoiceId,
                CompanyId = Guid7TestValues.CompanyA,
                SaleId = Guid7TestValues.SaleA,
                ENCF = Encf,
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = "130862346",
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m,
                Status = EcfStatus.Accepted,
                TrackId = "TRACK-1",
                SecurityCode = "SEC-1",
                QrUrl = QrUrl
            },
            new ElectronicInvoice
            {
                Id = _otherTenantInvoiceId,
                CompanyId = Guid7TestValues.CompanyB,
                SaleId = otherTenantSaleId,
                ENCF = OtherTenantEncf,
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = "130862354",
                Total = 100m,
                Status = EcfStatus.Accepted
            });

        await db.SaveChangesAsync();
    }
}
