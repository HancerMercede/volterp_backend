using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// The readiness of the taxpayer as the tenant sees it: the company comes
// from the token and never from the request.
[Collection("VolterpApiFactory")]
public class ElectronicInvoiceReadinessEndpointTests : IClassFixture<ApiFactory>
{
    private const string TaxIdOfA = "130862346";
    private const string TaxIdOfB = "130862354";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;

    public ElectronicInvoiceReadinessEndpointTests(ApiFactory factory)
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
    public async Task TheEndpointAnswersTheReadinessOfTheCompanyOfTheToken()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync("/api/ElectronicInvoices/readiness");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        root.GetProperty("emissionMode").GetString().Should().Be("Electronic");
        root.GetProperty("usesElectronicEmission").GetBoolean().Should().BeTrue();
        root.GetProperty("isReady").GetBoolean().Should().BeTrue();
        root.GetProperty("blockers").GetArrayLength().Should().Be(0);

        var consumption = root.GetProperty("documentTypes").EnumerateArray()
            .Single(type => type.GetProperty("code").GetString() == "E32");

        consumption.GetProperty("isIssuable").GetBoolean().Should().BeTrue();
        consumption.GetProperty("remaining").GetInt32().Should().Be(90);
    }

    [Fact]
    public async Task TheAnswerOfTheTenantNeverMentionsAnotherOne()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync("/api/ElectronicInvoices/readiness");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain(TaxIdOfB,
            "the readiness of the tenant never talks about another company");
    }

    [Fact]
    public async Task TheTenantWithoutCertificateAndRange_IsNotReady()
    {
        SetAuth(Guid7TestValues.CompanyB);

        var response = await _client.GetAsync("/api/ElectronicInvoices/readiness");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        root.GetProperty("isReady").GetBoolean().Should().BeFalse();
        root.GetProperty("hasValidCertificate").GetBoolean().Should().BeFalse();
        root.GetProperty("blockers").GetArrayLength().Should().Be(2,
            "the certificate and the range of the consumption are what the tenant is missing");
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
                TaxId = TaxIdOfA,
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
                TaxId = TaxIdOfB,
                Address = "2 B Street",
                LegalName = "Company B Legal",
                Email = "b@company.test",
                Phone = "555-0002",
                IsActive = true
            });

        // The certificate and the range belong to the first company only: the
        // second one has nothing, so it is not ready.
        db.CompanyCertificates.Add(new CompanyCertificate
        {
            Id = Guid.CreateVersion7(),
            CompanyId = Guid7TestValues.CompanyA,
            Name = "Certificate of A",
            TaxId = TaxIdOfA,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            EncryptedCredential = "psfe-api-key-DO-NOT-LEAK"
        });

        db.ElectronicInvoiceSequences.Add(new ElectronicInvoiceSequence
        {
            Id = Guid.CreateVersion7(),
            CompanyId = Guid7TestValues.CompanyA,
            IssuerTaxId = TaxIdOfA,
            DocumentType = EcfDocumentType.Consumer,
            From = 1,
            To = 100,
            CurrentSequence = 10
        });

        await db.SaveChangesAsync();
    }
}
