using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Application.Abstractions;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

public class CompanyCertificateIntegrationTests : IClassFixture<ApiFactory>
{
    private const string Credential = "psfe-api-key-DO-NOT-LEAK";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;

    public CompanyCertificateIntegrationTests(ApiFactory factory)
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
    public async Task TheApiNeverReturnsTheCredential_AndTheRowKeepsItEncrypted()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.PostAsync(
            "/api/CompanyCertificates",
            CreateBody("cert-encrypted", Guid7TestValues.CompanyB));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(Credential);
        body.Should().NotContain("v1:");
        body.Should().NotContain("ncryptedCredential");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        var options = new DbContextOptionsBuilder<VolterpDbContext>().UseSqlite(connection).Options;
        using var unrestricted = new VolterpDbContext(options, new NullTenantContext());

        var row = await unrestricted.CompanyCertificates.SingleAsync(c => c.Name == "cert-encrypted");
        row.CompanyId.Should().Be(Guid7TestValues.CompanyA,
            "the JWT decides the tenant, not the companyId sent in the body");
        row.EncryptedCredential.Should().StartWith("v1:");
        row.EncryptedCredential.Should().NotContain(Credential);
        row.TaxId.Should().Be("130862346", "the issuer RNC is persisted normalized to digits");
    }

    [Fact]
    public async Task AnotherTenantCanNeitherReadNorListTheCertificate()
    {
        SetAuth(Guid7TestValues.CompanyA);
        var created = await _client.PostAsync(
            "/api/CompanyCertificates",
            CreateBody("cert-isolation", Guid7TestValues.CompanyA));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetGuid();

        SetAuth(Guid7TestValues.CompanyB);

        var byId = await _client.GetAsync($"/api/CompanyCertificates/{id}");
        ((int)byId.StatusCode).Should().Be((int)HttpStatusCode.NotFound);

        var list = await _client.GetAsync("/api/CompanyCertificates");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await list.Content.ReadAsStringAsync();
        listBody.Should().NotContain("cert-isolation");
        listBody.Should().NotContain(Credential);
    }

    [Fact]
    public async Task ANonAdminCannotCreateACertificate()
    {
        SetAuth(Guid7TestValues.CompanyA, "Employee");

        var response = await _client.PostAsync(
            "/api/CompanyCertificates",
            CreateBody("cert-forbidden", Guid7TestValues.CompanyA));

        ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Forbidden);
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

        await db.SaveChangesAsync();
    }

    private void SetAuth(Guid companyId, string role = "Admin")
    {
        var token = JwtTestHelper.MintToken(_factory, companyId, Guid7TestValues.UserA, role);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static StringContent CreateBody(string name, Guid claimedCompanyId)
        => new(JsonSerializer.Serialize(new
        {
            name,
            taxId = "130-862346",
            expiresAt = DateTime.UtcNow.AddDays(30),
            credential = Credential,
            companyId = claimedCompanyId
        }), Encoding.UTF8, "application/json");
}
