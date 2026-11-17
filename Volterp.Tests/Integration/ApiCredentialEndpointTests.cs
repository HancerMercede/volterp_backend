using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volterp.Api.Helpers;
using Volterp.Application.Abstractions;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// The administration of the credentials of a client that rents the API: the
// secret is shown once and the tenant comes from the token.
[Collection("VolterpApiFactory")]
public class ApiCredentialEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;

    public ApiCredentialEndpointTests(ApiFactory factory)
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
    public async Task TheAdminCreatesACredentialAndTheAnswerCarriesTheSecret()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.PostAsync("/api/ApiCredentials", CreateBody("pos-of-a"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        root.GetProperty("keyId").GetString().Should().HaveLength(12);
        root.GetProperty("secret").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);
        body.Should().NotContain("secretHash", "the hash is not part of the answer");

        var row = await CredentialRowAsync(root.GetProperty("keyId").GetString()!);

        row.CompanyId.Should().Be(Guid7TestValues.CompanyA,
            "the company comes from the token, not from the body");
        row.SecretHash.Should().NotContain(root.GetProperty("secret").GetString());
        row.SecretHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task TheListNeverCarriesTheSecretNorTheHash()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var created = await _client.PostAsync("/api/ApiCredentials", CreateBody("listed-pos"));
        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var secret = JsonDocument.Parse(await created.Content.ReadAsStringAsync())
            .RootElement.GetProperty("secret").GetString();

        var response = await _client.GetAsync("/api/ApiCredentials");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("listed-pos");
        body.Should().NotContain(secret!);
        body.Should().NotContain("secretHash");
        body.Should().NotContain("SecretHash");
    }

    [Fact]
    public async Task ANonAdminCannotCreateACredential()
    {
        SetAuth(Guid7TestValues.CompanyA, "Employee");

        var response = await _client.PostAsync("/api/ApiCredentials", CreateBody("forbidden-pos"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheDeleteRevokesTheCredentialAndItStopsAuthenticating()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var created = await _client.PostAsync("/api/ApiCredentials", CreateBody("revoked-pos"));
        var issued = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement;
        var id = issued.GetProperty("id").GetGuid();
        var key = $"{issued.GetProperty("keyId").GetString()}.{issued.GetProperty("secret").GetString()}";

        var deleted = await _client.DeleteAsync($"/api/ApiCredentials/{id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var row = await CredentialRowAsync(issued.GetProperty("keyId").GetString()!);
        row.Status.Should().Be(EntityStatus.Inactive, "the row is not deleted: what was issued matters");

        using var machine = _factory.CreateClient();
        machine.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", key);

        var response = await machine.GetAsync("/api/Clients");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ACallWithAnApiKeyReachesTheApiWithTheTenantOfTheCredential()
    {
        SetAuth(Guid7TestValues.CompanyB);

        var created = await _client.PostAsync("/api/ApiCredentials", CreateBody("pos-of-b"));
        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var issued = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement;
        var key = $"{issued.GetProperty("keyId").GetString()}.{issued.GetProperty("secret").GetString()}";

        using var machine = _factory.CreateClient();
        machine.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", key);

        var response = await machine.GetAsync("/api/Clients");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("client-of-b");
        body.Should().NotContain("client-of-a",
            "the tenant of the call is the one of the credential");
    }

    [Fact]
    public async Task TheAllowedOriginsAreTheOnesOfTheConfiguration()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var allowed = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()!;

        allowed.Should().Contain("http://localhost:5173", "the front of the ERP keeps calling the API");

        using var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Add("Origin", allowed[0]);
        var accepted = await client.GetAsync("/api/ApiCredentials");
        accepted.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue(
            "the origin of the configuration is allowed");

        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Add("Origin", "https://not-allowed.example");
        var rejected = await client.GetAsync("/api/ApiCredentials");
        rejected.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse(
            "the origins that are not in the configuration are not allowed");
    }

    [Fact]
    public async Task TheSaleOfAMachineCompletesWithoutAUser()
    {
        SetAuth(Guid7TestValues.CompanyB);

        var created = await _client.PostAsync("/api/ApiCredentials", CreateBody("pos-of-the-sale"));
        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var issued = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement;
        var key = $"{issued.GetProperty("keyId").GetString()}.{issued.GetProperty("secret").GetString()}";

        using var machine = _factory.CreateClient();
        machine.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", key);

        var response = await machine.PutAsJsonAsync($"/api/Sales/{SaleOfCompanyB}/complete", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the audit of the sale takes the id of the user and a machine has no user");

        var sale = await SaleRowAsync(SaleOfCompanyB);
        sale.Status.Should().Be(SaleStatus.Completed);
        sale.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public async Task ACallWithoutCredentialsKeepsTheAnswerOfTheHouse()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/ApiCredentials");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().Should().Be("Bearer");
        body.Should().Contain("error", "the error of the API travels in the envelope of the house");
    }

    [Fact]
    public void ThePolicyTakesTheOriginsOfWhateverConfigurationItIsGiven()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://renter.example",
                ["Cors:AllowedOrigins:1"] = "https://pos.example"
            })
            .Build();

        var services = new ServiceCollection();
        ServiceExtensions.ConfigureCors(services, configuration);

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<CorsOptions>>().Value;

        options.GetPolicy(options.DefaultPolicyName)!.Origins.Should().BeEquivalentTo(
            new[] { "https://renter.example", "https://pos.example" },
            "the origins come from the configuration and not from a literal in the code");
    }

    private async Task<ApiCredential> CredentialRowAsync(string keyId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        var options = new DbContextOptionsBuilder<VolterpDbContext>().UseSqlite(connection).Options;

        using var unrestricted = new VolterpDbContext(options, new NullTenantContext());

        return await unrestricted.Set<ApiCredential>().SingleAsync(c => c.KeyId == keyId);
    }

    private async Task<Sale> SaleRowAsync(Guid saleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        var options = new DbContextOptionsBuilder<VolterpDbContext>().UseSqlite(connection).Options;

        using var unrestricted = new VolterpDbContext(options, new NullTenantContext());

        return await unrestricted.Sales.SingleAsync(s => s.Id == saleId);
    }

    private static StringContent CreateBody(string name)
        => new(JsonSerializer.Serialize(new
        {
            name,
            expiresAt = DateTime.UtcNow.AddDays(30)
        }), Encoding.UTF8, "application/json");

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

        db.Clients.AddRange(
            new Client
            {
                Id = Guid.CreateVersion7(),
                CompanyId = Guid7TestValues.CompanyA,
                Code = "CLI-A",
                Name = "client-of-a",
                Email = "a@client.test",
                Phone = "555-1001",
                Address = "1 A Street"
            },
            new Client
            {
                Id = Guid.CreateVersion7(),
                CompanyId = Guid7TestValues.CompanyB,
                Code = "CLI-B",
                Name = "client-of-b",
                Email = "b@client.test",
                Phone = "555-2001",
                Address = "2 B Street"
            });

        // The sale of the tenant of the machine: it is completed by the
        // machine and its audit goes without a user.
        var sale = new Sale
        {
            Id = SaleOfCompanyB,
            CompanyId = Guid7TestValues.CompanyB,
            Code = "V-0001",
            Status = SaleStatus.Pending
        };
        sale.AddItem(Guid.CreateVersion7(), "Item of the sale", 1, 100m, TaxCode.NoFacturable);
        db.Sales.Add(sale);

        await db.SaveChangesAsync();
    }

    private static readonly Guid SaleOfCompanyB = Guid.CreateVersion7();
}
