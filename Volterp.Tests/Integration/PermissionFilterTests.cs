using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Volterp.Domain.Enums;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// The filter that authorizes every route: the route says what it needs (the
// map of the routes) and the matrix says which roles have it.
[Collection("VolterpApiFactory")]
public class PermissionFilterTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public PermissionFilterTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.EnsureSchemaCreated();
    }

    [Fact]
    public async Task TheCallerThatHoldsTheRoleOfTheRouteGetsIn()
    {
        SetAuth(nameof(UserRole.Ventas));

        var response = await _client.GetAsync("/api/Sales");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheCallerThatDoesNotHoldTheRoleOfTheRouteIsRefused()
    {
        SetAuth(nameof(UserRole.Inventario));

        var response = await _client.GetAsync("/api/Sales");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheRefusalNamesThePermissionThatIsMissing()
    {
        SetAuth(nameof(UserRole.Inventario));

        var response = await _client.GetAsync("/api/Sales");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetProperty("error").GetString()
            .Should().Contain("ventas.read",
                "a caller that does not know which permission is missing cannot do anything with the refusal");
    }

    [Fact]
    public async Task TheCallerWithTwoRolesHasThePermissionsOfBoth()
    {
        SetAuth(nameof(UserRole.Ventas), nameof(UserRole.Inventario));

        var sales = await _client.GetAsync("/api/Sales");
        var products = await _client.GetAsync("/api/Products");

        sales.StatusCode.Should().Be(HttpStatusCode.OK);
        products.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheAdminKeepsEveryRoute()
    {
        SetAuth(nameof(UserRole.SuperAdmin));

        var sales = await _client.GetAsync("/api/Sales");
        var accounting = await _client.GetAsync("/api/AccountingTransactions");
        var users = await _client.GetAsync("/api/Users");

        sales.StatusCode.Should().Be(HttpStatusCode.OK);
        accounting.StatusCode.Should().Be(HttpStatusCode.OK);
        users.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheRouteOfTheLoginIsNotTheUsers()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        // The login is public: it is not a route of the map, and the filter
        // does not touch the routes that a token does not protect.
        var response = await _client.PostAsJsonAsync("/api/Auth/login",
            new { username = "nobody", password = "nothing" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private void SetAuth(params string[] roles)
    {
        var token = JwtTestHelper.MintToken(_factory, Guid7TestValues.CompanyA, Guid7TestValues.UserA, roles);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
