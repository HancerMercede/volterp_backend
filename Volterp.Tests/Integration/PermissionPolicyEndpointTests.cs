using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// The authorization of a request: the endpoint declares the permission it
// needs and the caller is let in when the roles of its token grant it.
[Collection("VolterpApiFactory")]
public class PermissionPolicyEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public PermissionPolicyEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.EnsureSchemaCreated();
    }

    [Fact]
    public async Task TheCallerThatHoldsTheRoleIsLetIn()
    {
        SetAuth("Ventas");

        var response = await _client.GetAsync("/api/test/permissions/sales-read");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheCallerThatDoesNotHoldTheRoleIsRefused()
    {
        SetAuth("Ventas");

        var response = await _client.GetAsync("/api/test/permissions/inventory-read");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheRefusalNamesThePermissionThatIsMissing()
    {
        SetAuth("Ventas");

        var response = await _client.GetAsync("/api/test/permissions/inventory-read");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetProperty("error").GetString()
            .Should().Contain("inventario.read",
                "a caller that does not know which permission is missing cannot do anything with the refusal");
    }

    [Fact]
    public async Task TheCallerWithTwoRolesHasThePermissionsOfBoth()
    {
        SetAuth("Ventas", "Inventario");

        var sales = await _client.GetAsync("/api/test/permissions/sales-read");
        var inventory = await _client.GetAsync("/api/test/permissions/inventory-read");

        sales.StatusCode.Should().Be(HttpStatusCode.OK);
        inventory.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheCallerWithoutATokenIsNotEvenAsked()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/test/permissions/sales-read");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private void SetAuth(params string[] roles)
    {
        var token = JwtTestHelper.MintToken(_factory, Guid7TestValues.CompanyA, Guid7TestValues.UserA, roles);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
