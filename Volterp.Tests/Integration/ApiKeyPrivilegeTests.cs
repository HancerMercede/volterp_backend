using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// A machine that rents the API is not a user of the ERP: it creates sales,
// not users, and it reaches nothing that an administrator alone reaches.
[Collection("VolterpApiFactory")]
public class ApiKeyPrivilegeTests : IClassFixture<ApiFactory>
{
    private static readonly Guid CompanyOfTheMachine = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d90");

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;

    public ApiKeyPrivilegeTests(ApiFactory factory)
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
    public async Task AMachineCannotRegisterAnAdminOfItsTenant()
    {
        var key = await IssueAKeyAsync();

        using var machine = _factory.CreateClient();
        machine.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", key);

        var response = await machine.PostAsJsonAsync("/api/auth/register", new
        {
            username = "machine-made-admin",
            password = "MachineMadeAdmin1!",
            email = "machine@company.test",
            fullName = "Machine Made Admin",
            role = "Admin"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a machine does not create users of the ERP");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        db.Users.Should().NotContain(u => u.Username == "machine-made-admin");
    }

    [Fact]
    public async Task ANonAdminCannotListTheCredentials()
    {
        SetAuth(CompanyOfTheMachine, "Employee");

        var response = await _client.GetAsync("/api/ApiCredentials");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheForbiddenOfAMachineCarriesTheErrorEnvelope()
    {
        var key = await IssueAKeyAsync();

        using var machine = _factory.CreateClient();
        machine.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", key);

        var response = await machine.PostAsJsonAsync("/api/ApiCredentials", new { name = "machine-credential" });
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should().Contain("Forbidden.", "the answer of the API travels in the envelope of the house");
    }

    [Fact]
    public async Task AnInvalidJwtKeepsTheHouseAnswer()
    {
        using var anonymous = _factory.CreateClient();
        anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "garbage");

        var response = await anonymous.GetAsync("/api/Clients");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().Should().Be("Bearer");
        body.Should().Contain("error");
    }

    private async Task<string> IssueAKeyAsync()
    {
        SetAuth(CompanyOfTheMachine);

        var created = await _client.PostAsJsonAsync("/api/ApiCredentials", new { name = "privilege-pos" });
        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var issued = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement;

        return $"{issued.GetProperty("keyId").GetString()}.{issued.GetProperty("secret").GetString()}";
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

        if (await db.Companies.AnyAsync(c => c.Id == CompanyOfTheMachine))
            return;

        db.Companies.Add(new Company
        {
            Id = CompanyOfTheMachine,
            Name = "Company of the machine",
            LegalName = "Company of the machine SRL",
            TaxId = "130862361",
            Address = "3 C Street",
            Email = "c@company.test",
            Phone = "555-0003",
            IsActive = true
        });

        await db.SaveChangesAsync();
    }
}
