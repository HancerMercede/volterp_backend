using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// The taxpayer loads the range the DGII authorized for the normal documents
// and reads it back. The company always comes from the token.
[Collection("VolterpApiFactory")]
public class NormalFiscalSequenceEndpointTests : IClassFixture<ApiFactory>
{
    private const string IssuerTaxId = "130862346";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;

    public NormalFiscalSequenceEndpointTests(ApiFactory factory)
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
    public async Task TheAdministratorLoadsTheRangeOfTheCompanyOfTheToken()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.PostAsJsonAsync(
            "/api/NormalFiscalSequences",
            new
            {
                issuerTaxId = IssuerTaxId,
                documentType = "Consumer",
                from = 1,
                to = 10_000_000,
                expiresAt = (DateTime?)null,
                position = 1
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("issuerTaxId").GetString().Should().Be(IssuerTaxId);
        body.RootElement.GetProperty("documentType").GetString().Should().Be("Consumer");
        body.RootElement.GetProperty("from").GetInt32().Should().Be(1);
        body.RootElement.GetProperty("to").GetInt32().Should().Be(10_000_000);
        body.RootElement.GetProperty("currentSequence").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task TheAdministratorReadsTheRangesOfTheCompanyOfTheToken()
    {
        SetAuth(Guid7TestValues.CompanyA);
        await LoadRangeAsync(100);

        var response = await _client.GetAsync("/api/NormalFiscalSequences");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();

        items.Should().NotBeEmpty();
        items.Should().OnlyContain(item => item.GetProperty("issuerTaxId").GetString() == IssuerTaxId);
    }

    [Fact]
    public async Task TheRangeOfAnotherCompanyIsNotRead()
    {
        SetAuth(Guid7TestValues.CompanyB);

        var response = await _client.GetAsync("/api/NormalFiscalSequences");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task TheRangeOfAnotherCompanyIsNotFound()
    {
        SetAuth(Guid7TestValues.CompanyB);

        var response = await _client.GetAsync($"/api/NormalFiscalSequences/{Guid.CreateVersion7()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ANonAdminCannotLoadARange()
    {
        SetAuth(Guid7TestValues.CompanyA, "Employee");

        var response = await _client.PostAsJsonAsync(
            "/api/NormalFiscalSequences",
            new
            {
                issuerTaxId = IssuerTaxId,
                documentType = "Consumer",
                from = 1,
                to = 10_000_000,
                expiresAt = (DateTime?)null,
                position = 1
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ARangeThatGoesBeyondTheEightDigitsOfTheNumberIsRejected()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.PostAsJsonAsync(
            "/api/NormalFiscalSequences",
            new
            {
                issuerTaxId = IssuerTaxId,
                documentType = "Consumer",
                from = 1,
                to = NormalFiscalSequence.MaxSequence + 1,
                expiresAt = (DateTime?)null,
                position = 1
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task LoadRangeAsync(int from)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        var exists = await db.NormalFiscalSequences
            .IgnoreQueryFilters()
            .AnyAsync(range => range.CompanyId == Guid7TestValues.CompanyA && range.From == from);

        if (exists)
            return;

        db.NormalFiscalSequences.Add(new NormalFiscalSequence
        {
            Id = Guid.CreateVersion7(),
            CompanyId = Guid7TestValues.CompanyA,
            IssuerTaxId = IssuerTaxId,
            DocumentType = NormalFiscalDocumentType.Consumer,
            From = from,
            To = 10_000_000,
            CurrentSequence = 0
        });

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
                TaxId = IssuerTaxId,
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

        await db.SaveChangesAsync();
    }
}
