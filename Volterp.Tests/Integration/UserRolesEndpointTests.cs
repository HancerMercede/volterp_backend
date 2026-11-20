using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// The roles of a user: they are many, the set that is asked for is the whole
// truth, and the role that administers the system is not managed by an admin.
[Collection("VolterpApiFactory")]
public class UserRolesEndpointTests : IClassFixture<ApiFactory>
{
    private const string Password = "Passw0rd!";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;
    private static Guid _userId;

    public UserRolesEndpointTests(ApiFactory factory)
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
    public async Task TheRolesOfTheUserBecomeTheOnesThatWereAskedFor()
    {
        await ResetRolesAsync(UserRole.Vendedor);
        SetAuth(nameof(UserRole.SuperAdmin));

        var response = await _client.PutAsJsonAsync($"/api/Users/{_userId}/roles",
            new { roles = new[] { "Vendedor", "Inventario" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await RolesInTheDatabaseAsync())
            .Should().BeEquivalentTo([UserRole.Vendedor, UserRole.Inventario]);
    }

    [Fact]
    public async Task TheRoleTheUserAlreadyHoldsIsNotDuplicated()
    {
        await ResetRolesAsync(UserRole.Vendedor);
        SetAuth(nameof(UserRole.SuperAdmin));

        var response = await _client.PutAsJsonAsync($"/api/Users/{_userId}/roles",
            new { roles = new[] { "Vendedor" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await RolesInTheDatabaseAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task AnAdminCannotGrantTheSuperAdminRole()
    {
        await ResetRolesAsync(UserRole.Vendedor);
        SetAuth(nameof(UserRole.Admin));

        var response = await _client.PutAsJsonAsync($"/api/Users/{_userId}/roles",
            new { roles = new[] { "Vendedor", "SuperAdmin" } });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await RolesInTheDatabaseAsync()).Should().BeEquivalentTo([UserRole.Vendedor]);
    }

    [Fact]
    public async Task AnAdminCannotTakeTheSuperAdminRoleAway()
    {
        await ResetRolesAsync(UserRole.SuperAdmin);
        SetAuth(nameof(UserRole.Admin));

        var response = await _client.PutAsJsonAsync($"/api/Users/{_userId}/roles",
            new { roles = new[] { "Vendedor" } });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await RolesInTheDatabaseAsync()).Should().BeEquivalentTo([UserRole.SuperAdmin]);
    }

    [Fact]
    public async Task AUserThatIsNotAnAdminCannotChangeTheRoles()
    {
        await ResetRolesAsync(UserRole.Vendedor);
        SetAuth(nameof(UserRole.Vendedor));

        var response = await _client.PutAsJsonAsync($"/api/Users/{_userId}/roles",
            new { roles = new[] { "Vendedor", "RRHH" } });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await RolesInTheDatabaseAsync()).Should().BeEquivalentTo([UserRole.Vendedor]);
    }

    [Fact]
    public async Task TheRolesOfAUserCannotBeEmpty()
    {
        await ResetRolesAsync(UserRole.Vendedor);
        SetAuth(nameof(UserRole.SuperAdmin));

        var response = await _client.PutAsJsonAsync($"/api/Users/{_userId}/roles",
            new { roles = Array.Empty<string>() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await RolesInTheDatabaseAsync()).Should().BeEquivalentTo([UserRole.Vendedor]);
    }

    [Fact]
    public async Task TheLoginCarriesEveryRoleOfTheUser()
    {
        await ResetRolesAsync(UserRole.Vendedor, UserRole.Inventario);

        var response = await _client.PostAsJsonAsync("/api/Auth/login",
            new { username = "roles-user", password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        root.GetProperty("roles").EnumerateArray().Select(role => role.GetString())
            .Should().BeEquivalentTo(["Vendedor", "Inventario"],
                "the front reads the roles of the user from the answer of the login");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(root.GetProperty("token").GetString()!);
        var claims = token.Claims.Where(claim => claim.Type == "role" || claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value).ToList();

        claims.Should().BeEquivalentTo(["Vendedor", "Inventario"],
            "the token carries one claim per role: the permissions of the user are their union");
    }

    private void SetAuth(string role)
    {
        var token = JwtTestHelper.MintToken(_factory, Guid7TestValues.CompanyA, Guid7TestValues.UserA, role);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<UserRole[]> RolesInTheDatabaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        var user = await db.Users.IgnoreQueryFilters()
            .Include(candidate => candidate.Roles)
            .FirstAsync(candidate => candidate.Id == _userId);

        return user.Roles.Select(assignment => assignment.Role).ToArray();
    }

    private async Task ResetRolesAsync(params UserRole[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        var user = await db.Users.IgnoreQueryFilters()
            .Include(candidate => candidate.Roles)
            .FirstAsync(candidate => candidate.Id == _userId);

        db.UserRoles.RemoveRange(user.Roles);
        await db.SaveChangesAsync();

        foreach (var role in roles)
        {
            db.UserRoles.Add(new UserRoleAssignment
            {
                Id = Guid.CreateVersion7(),
                UserId = _userId,
                CompanyId = Guid7TestValues.CompanyA,
                Role = role
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        db.Companies.Add(new Company
        {
            Id = Guid7TestValues.CompanyA,
            Name = "Company A",
            TaxId = "130862346",
            Address = "1 A Street",
            LegalName = "Company A Legal",
            Email = "a@company.test",
            Phone = "809-555-1234",
            IsActive = true
        });

        _userId = Guid.CreateVersion7();

        var user = new User
        {
            Id = _userId,
            CompanyId = Guid7TestValues.CompanyA,
            Code = "U-0001",
            Username = "roles-user",
            Email = "roles@company.test",
            FullName = "Roles User",
            PasswordHash = hasher.Hash(Password),
            IsActive = true
        };

        user.AssignRole(UserRole.Vendedor);

        db.Users.Add(user);

        await db.SaveChangesAsync();
    }
}
