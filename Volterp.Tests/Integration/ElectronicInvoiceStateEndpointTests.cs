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

// The surface of the fiscal state as the tenant sees it: the company comes
// from the token and never from the request.
[Collection("VolterpApiFactory")]
public class ElectronicInvoiceStateEndpointTests : IClassFixture<ApiFactory>
{
    private const string PendingEncf = "E320000000001";
    private const string OtherTenantEncf = "E320000000002";
    private const string RejectedEncf = "E320000000003";
    private const string RejectionReason = "El RNC del receptor no existe.";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;

    public ElectronicInvoiceStateEndpointTests(ApiFactory factory)
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
    public async Task TheEndpointTakesTheCompanyFromTheToken()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync("/api/ElectronicInvoices/state");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(PendingEncf, "the document of the company of the token is listed");
        body.Should().NotContain(OtherTenantEncf, "the document of another company is never listed");
    }

    [Fact]
    public async Task TheEndpointAnswersTheCountAndTheList()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync("/api/ElectronicInvoices/state");
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        root.GetProperty("counts").GetProperty("pendingEmission").GetInt32().Should().Be(1);
        root.GetProperty("counts").GetProperty("rejected").GetInt32().Should().Be(1);
        root.GetProperty("counts").GetProperty("total").GetInt32().Should().Be(2);

        var invoices = root.GetProperty("invoices").EnumerateArray().ToList();
        invoices.Should().HaveCount(2);

        var pending = invoices.Single(i => i.GetProperty("encf").GetString() == PendingEncf);
        pending.GetProperty("saleId").GetGuid().Should().Be(Guid7TestValues.SaleA,
            "the front joins the fiscal state with the sale on the screen");
        pending.GetProperty("fiscalState").GetString().Should().Be("PendingEmission");
        pending.GetProperty("status").GetString().Should().Be("Signed");

        var rejected = invoices.Single(i => i.GetProperty("encf").GetString() == RejectedEncf);
        rejected.GetProperty("verdictNote").GetString().Should().Be(RejectionReason);
    }

    [Fact]
    public async Task TheCountCoversEveryOpenDocument_EvenWhenTheListIsLimited()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync("/api/ElectronicInvoices/state?limit=1");
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        root.GetProperty("invoices").GetArrayLength().Should().Be(1);
        root.GetProperty("counts").GetProperty("total").GetInt32().Should().Be(2,
            "the count covers all the open documents, not only the listed ones");
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

        var rejectedSaleId = Guid.CreateVersion7();
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
                Id = rejectedSaleId,
                CompanyId = Guid7TestValues.CompanyA,
                Code = "V-0002",
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
                Code = "V-0003",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            });

        db.ElectronicInvoices.AddRange(
            new ElectronicInvoice
            {
                Id = Guid.CreateVersion7(),
                CompanyId = Guid7TestValues.CompanyA,
                SaleId = Guid7TestValues.SaleA,
                ENCF = PendingEncf,
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = "130862346",
                SignedXml = "<ECF />",
                Status = EcfStatus.Signed
            },
            new ElectronicInvoice
            {
                Id = Guid.CreateVersion7(),
                CompanyId = Guid7TestValues.CompanyA,
                SaleId = rejectedSaleId,
                ENCF = RejectedEncf,
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = "130862346",
                SignedXml = "<ECF />",
                Status = EcfStatus.Rejected,
                VerdictNote = RejectionReason
            },
            new ElectronicInvoice
            {
                Id = Guid.CreateVersion7(),
                CompanyId = Guid7TestValues.CompanyB,
                SaleId = otherTenantSaleId,
                ENCF = OtherTenantEncf,
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = "130862354",
                SignedXml = "<ECF />",
                Status = EcfStatus.Signed
            });

        await db.SaveChangesAsync();
    }
}
