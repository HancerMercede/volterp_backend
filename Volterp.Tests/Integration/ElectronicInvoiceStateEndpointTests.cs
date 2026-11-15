using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Migrations;
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
    private const string WithTokenEncf = "E320000000004";
    private const string CutEncf = "E320000000005";
    private const string RejectionReason = "El RNC del receptor no existe.";
    private const string TokenOfTheDgii = "eyJhbGciOiJIUzI1NiJ9.dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

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

    [Fact]
    public async Task TheTokenOfTheDgiiNeverComesOut()
    {
        SetAuth(Guid7TestValues.CompanyB);

        var response = await _client.GetAsync("/api/ElectronicInvoices/state");
        var body = await response.Content.ReadAsStringAsync();

        // The token of the DGII is a credential: the state of the tenant is
        // not the place where it travels.
        body.Should().NotContain(TokenOfTheDgii);
    }

    [Fact]
    public async Task TheBodyOfTheDgiiThatWasCutSaysSo()
    {
        SetAuth(Guid7TestValues.CompanyB);

        var response = await _client.GetAsync("/api/ElectronicInvoices/state");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var invoice = document.RootElement.GetProperty("invoices").EnumerateArray()
            .Single(i => i.GetProperty("encf").GetString() == CutEncf);

        var raw = invoice.GetProperty("rawResponse").GetString();

        // The row cannot grow without limit, and the cut is visible: the
        // front has to know that the answer it shows is not the whole one.
        raw!.Length.Should().BeLessThan(5_000);
        raw.Should().EndWith("[truncated]");
    }

    [Fact]
    public void TheDownOfTheMigrationOfTheResponsesDropsTheColumnsThatHoldThem()
    {
        // The migration of T47 added the four columns with what the DGII
        // answered, and its Down drops them: the answers are gone with them.
        var migration = new AddDgiiResponses();
        var builder = new MigrationBuilder("Npgsql");

        typeof(AddDgiiResponses)
            .GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        var dropped = builder.Operations.OfType<DropColumnOperation>().Select(o => o.Name);

        dropped.Should().Contain(["RawResponse", "SequenceConsumed", "ReceivedAt", "LastError"]);
    }

    [Fact]
    public void TheReadmeSaysThatTheDownOfThatMigrationDeletesTheAnswers()
    {
        // The rollback destroys what the DGII answered: whoever reverts it
        // has to know it before, not after.
        var readme = Readme();

        readme.Should().Contain("20261114221020_AddDgiiResponses");
        readme.Should().Contain("borra las respuestas registradas");
    }

    private static string Readme()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Volterp.slnx")))
            directory = directory.Parent;

        return File.ReadAllText(Path.Combine(directory!.FullName, "README.md"));
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
        var tokenSaleId = Guid.CreateVersion7();
        var cutSaleId = Guid.CreateVersion7();

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
            },
            new Sale
            {
                Id = tokenSaleId,
                CompanyId = Guid7TestValues.CompanyB,
                Code = "V-0004",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            },
            new Sale
            {
                Id = cutSaleId,
                CompanyId = Guid7TestValues.CompanyB,
                Code = "V-0005",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            });

        // The answer of a gateway that answers with a pathological body: it
        // is cut when it goes on record.
        var cutInvoice = new ElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = Guid7TestValues.CompanyB,
            SaleId = cutSaleId,
            ENCF = CutEncf,
            DocumentType = EcfDocumentType.Consumer,
            IssuerTaxId = "130862354",
            SignedXml = "<ECF />",
            Status = EcfStatus.Signed
        };

        cutInvoice.RecordDgiiResponse(new string('x', 50_000));

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
            },
            new ElectronicInvoice
            {
                Id = Guid.CreateVersion7(),
                CompanyId = Guid7TestValues.CompanyB,
                SaleId = tokenSaleId,
                ENCF = WithTokenEncf,
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = "130862354",
                SignedXml = "<ECF />",
                Status = EcfStatus.InProcess,
                // The body of the conversation of the token can carry the
                // token of the DGII: it is a credential.
                RawResponse = $$"""{"token":"{{TokenOfTheDgii}}","expira":"2026-11-14T11:00:00"}"""
            },
            cutInvoice);

        await db.SaveChangesAsync();
    }
}
