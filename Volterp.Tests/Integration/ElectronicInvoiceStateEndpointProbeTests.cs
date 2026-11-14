using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

// The probes of the fiscal surface as the tenant sees it: the company comes
// from the token and never from the request, and the limit is answered with
// the message the front shows.
[Collection("VolterpApiFactory")]
public class ElectronicInvoiceStateEndpointProbeTests : IClassFixture<ApiFactory>
{
    private const string LimitMessage = "limit must be greater than zero.";
    private const string PendingEncf = "E320000000001";
    private const string RejectedEncf = "E320000000002";
    private const string OtherTenantEncf = "E320000000003";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;

    public ElectronicInvoiceStateEndpointProbeTests(ApiFactory factory)
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
    public async Task TheCompanyOfTheQueryIsIgnored()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync(
            $"/api/ElectronicInvoices/state?companyId={Guid7TestValues.CompanyB}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(PendingEncf, "the company of the token is the one that is answered");
        body.Should().NotContain(OtherTenantEncf, "the company of the query does not change the answer");
    }

    [Fact]
    public async Task TheCompanyOfTheHeaderIsIgnored()
    {
        SetAuth(Guid7TestValues.CompanyA);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/ElectronicInvoices/state");
        request.Headers.Add("X-CompanyId", Guid7TestValues.CompanyB.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(PendingEncf, "the company of the token is the one that is answered");
        body.Should().NotContain(OtherTenantEncf, "the company of the header does not change the answer");
    }

    [Fact]
    public async Task TheCompanyOfTheBodyIsIgnored()
    {
        SetAuth(Guid7TestValues.CompanyA);

        // The surface is a query: the company cannot arrive in the body.
        var response = await _client.PostAsJsonAsync(
            "/api/ElectronicInvoices/state",
            new { companyId = Guid7TestValues.CompanyB });

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(-2147483648)]
    public async Task ALimitThatIsNotPositive_AnswersBadRequestWithItsMessage(int limit)
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/ElectronicInvoices/state?limit={limit}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(LimitMessage);
    }

    [Fact]
    public async Task ALimitOfIntMaxValue_AnswersOk()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync("/api/ElectronicInvoices/state?limit=2147483647");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ALimitThatIsNotANumber_AnswersBadRequest()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync("/api/ElectronicInvoices/state?limit=abc");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("limit", "the front has to know which parameter it got wrong");
    }

    [Fact]
    public async Task ALimitOfOneWithTwoOpenDocuments_AnswersTheWholeCountAndOneDocument()
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

    [Fact]
    public async Task ATokenWithoutACompany_IsNotAuthorized()
    {
        var token = JwtTestHelper.MintTokenWithoutCompany(_factory, Guid7TestValues.UserA);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/ElectronicInvoices/state");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
                VerdictNote = "El RNC del receptor no existe."
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
