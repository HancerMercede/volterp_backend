using System.Net;
using System.Net.Http.Headers;
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

// Independent probe of T47 (read-only verifier): what the tenant sees by HTTP is
// what stayed in the database, and a tenant never sees the answer of another.
[Collection("VolterpApiFactory")]
public class T47SurfaceProbeTests : IClassFixture<ApiFactory>
{
    private const string EncfOfA = "E310000000901";
    private const string EncfOfB = "E310000000902";
    private const string RawBodyOfA =
        """{"codigo":2,"estado":"Rechazado","secuenciaUtilizada":true,"mensajes":[{"valor":"El RNC del receptor no existe","codigo":7}]}""";

    private static readonly DateTime ReceivedAtOfA = new(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;

    public T47SurfaceProbeTests(ApiFactory factory)
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
    public async Task TheSurfaceShowsWhatStayedInTheDatabase()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync("/api/ElectronicInvoices/state");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var invoice = document.RootElement.GetProperty("invoices").EnumerateArray()
            .Single(i => i.GetProperty("encf").GetString() == EncfOfA);

        invoice.GetProperty("rawResponse").GetString().Should().Be(RawBodyOfA);
        invoice.GetProperty("sequenceConsumed").GetBoolean().Should().BeTrue();
        invoice.GetProperty("receivedAt").GetDateTime().Should().Be(ReceivedAtOfA);
    }

    [Fact]
    public async Task AnotherTenantNeverSeesTheAnswerOfTheFirst()
    {
        SetAuth(Guid7TestValues.CompanyB);

        var response = await _client.GetAsync("/api/ElectronicInvoices/state");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain(EncfOfA, "the document of another company is never listed");
        body.Should().NotContain(RawBodyOfA, "the answer of another company is never exposed");
    }

    [Fact]
    public async Task WhatTheSurfaceShowsIsWhatTheDatabaseHolds()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
        var stored = await db.ElectronicInvoices
            .IgnoreQueryFilters()
            .SingleAsync(i => i.CompanyId == Guid7TestValues.CompanyA && i.ENCF == EncfOfA);

        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync("/api/ElectronicInvoices/state");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var invoice = document.RootElement.GetProperty("invoices").EnumerateArray()
            .Single(i => i.GetProperty("encf").GetString() == EncfOfA);

        invoice.GetProperty("rawResponse").GetString().Should().Be(stored.RawResponse);
        invoice.GetProperty("sequenceConsumed").GetBoolean().Should().Be(stored.SequenceConsumed == true);
        invoice.GetProperty("receivedAt").GetDateTime().Should().Be(stored.ReceivedAt);
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

        var saleOfA = Guid.CreateVersion7();
        var saleOfB = Guid.CreateVersion7();

        db.Sales.AddRange(
            new Sale
            {
                Id = saleOfA,
                CompanyId = Guid7TestValues.CompanyA,
                Code = "V-9001",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                Subtotal = 200m,
                Itbis = 36m,
                Total = 236m
            },
            new Sale
            {
                Id = saleOfB,
                CompanyId = Guid7TestValues.CompanyB,
                Code = "V-9002",
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
                SaleId = saleOfA,
                ENCF = EncfOfA,
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = "130862346",
                SignedXml = "<ECF />",
                Status = EcfStatus.Rejected,
                VerdictNote = "El RNC del receptor no existe.",
                RawResponse = RawBodyOfA,
                SequenceConsumed = true,
                ReceivedAt = ReceivedAtOfA
            },
            new ElectronicInvoice
            {
                Id = Guid.CreateVersion7(),
                CompanyId = Guid7TestValues.CompanyB,
                SaleId = saleOfB,
                ENCF = EncfOfB,
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = "130862354",
                SignedXml = "<ECF />",
                Status = EcfStatus.Signed
            });

        await db.SaveChangesAsync();
    }
}
