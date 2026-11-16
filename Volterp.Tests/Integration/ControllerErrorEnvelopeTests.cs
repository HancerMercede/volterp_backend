using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// Every error of the API travels in the same envelope: the status says what
// happened and the body says why, always with the same shape.
[Collection("VolterpApiFactory")]
public class ControllerErrorEnvelopeTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ControllerErrorEnvelopeTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.EnsureSchemaCreated();
    }

    [Fact]
    public async Task TheNotFoundOfAControllerCarriesTheErrorEnvelope()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.GetAsync($"/api/Clients/{Guid.CreateVersion7()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorOfAsync(response)).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TheBadRequestOfAControllerCarriesTheErrorEnvelope()
    {
        SetAuth(Guid7TestValues.CompanyA);

        var response = await _client.PostAsJsonAsync("/api/Clients", new
        {
            name = "",
            email = "client@company.test",
            phone = "555-0002",
            address = "1 A Street",
            imageUrl = (string?)null,
            isActive = true,
            createdAt = (DateTime?)null,
            updatedAt = (DateTime?)null,
            createdBy = (Guid?)null,
            updatedBy = (Guid?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorOfAsync(response)).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TheForbiddenOfAControllerCarriesTheErrorEnvelope()
    {
        SetAuth(Guid7TestValues.CompanyA, "Employee");

        var response = await _client.GetAsync("/api/Companies");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ErrorOfAsync(response)).Should().NotBeNullOrEmpty();
    }

    private async Task<string?> ErrorOfAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return body.RootElement.TryGetProperty("error", out var error)
            ? error.GetString()
            : null;
    }

    private void SetAuth(Guid companyId, string role = "Admin")
    {
        var token = JwtTestHelper.MintToken(_factory, companyId, Guid7TestValues.UserA, role);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
