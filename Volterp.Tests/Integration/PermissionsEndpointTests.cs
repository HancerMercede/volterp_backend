using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Volterp.Domain.Enums;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// What the caller may do: the front reads it from here and stops keeping a
// matrix of its own (S6).
[Collection("VolterpApiFactory")]
public class PermissionsEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public PermissionsEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.EnsureSchemaCreated();
    }

    [Fact]
    public async Task TheCallerLearnsWhatItMayDo()
    {
        SetAuth(nameof(UserRole.Ventas));

        var response = await _client.GetAsync("/api/permissions/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        root.GetProperty("roles").EnumerateArray().Select(role => role.GetString())
            .Should().BeEquivalentTo(["Ventas"]);

        var permissions = root.GetProperty("permissions").EnumerateArray()
            .Select(permission => permission.GetString())
            .ToArray();

        permissions.Should().Contain("ventas.read");
        permissions.Should().Contain("ventas.write");
        permissions.Should().NotContain("ventas.delete");
    }

    [Fact]
    public async Task ThePermissionsOfTwoRolesAreTheUnionOfBoth()
    {
        SetAuth(nameof(UserRole.Ventas), nameof(UserRole.Inventario));

        var response = await _client.GetAsync("/api/permissions/me");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var permissions = document.RootElement.GetProperty("permissions").EnumerateArray()
            .Select(permission => permission.GetString())
            .ToArray();

        permissions.Should().Contain("ventas.read");
        permissions.Should().Contain("inventario.read");
    }

    [Fact]
    public async Task TheMatrixIsForTheAdmins()
    {
        SetAuth(nameof(UserRole.SuperAdmin));

        var response = await _client.GetAsync("/api/permissions/roles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetProperty("roles").EnumerateArray().Should().HaveCount(6);
    }

    [Fact]
    public async Task TheCallerThatIsNotAnAdminDoesNotSeeTheMatrix()
    {
        SetAuth(nameof(UserRole.Ventas));

        var response = await _client.GetAsync("/api/permissions/roles");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheCallerWithoutATokenIsNotEvenAsked()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/permissions/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private void SetAuth(params string[] roles)
    {
        var token = JwtTestHelper.MintToken(_factory, Guid7TestValues.CompanyA, Guid7TestValues.UserA, roles);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
