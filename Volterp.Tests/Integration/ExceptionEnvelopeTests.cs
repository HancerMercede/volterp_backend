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

// The template decides the shape of the answer: 4xx travels in ErrorResponse and
// 5xx travels in RFC 7807 ProblemDetails. The middleware is where both of them
// are born, and the 401/403 the framework raises belong to the same envelope.
[Collection("VolterpApiFactory")]
public class ExceptionEnvelopeTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly object SeedLock = new();
    private static bool _seeded;

    public ExceptionEnvelopeTests(ApiFactory factory)
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
    public async Task TheClientErrorOfTheMiddlewareCarriesTheErrorEnvelope()
    {
        // A token without the company claim: the controller cannot resolve the
        // tenant and the failure leaves the action as an exception.
        var token = JwtTestHelper.MintTokenWithoutCompany(_factory, Guid7TestValues.UserA);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/suppliers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        (await ErrorOfAsync(response)).Should().Be("Missing companyId claim.");
    }

    [Fact]
    public async Task TheServerErrorOfTheMiddlewareCarriesProblemDetails()
    {
        // The endpoint of the test suite that throws and nobody handles: the
        // only way to reach the 5xx branch of the middleware.
        var response = await _client.GetAsync("/test/unhandled/throw");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetInt32().Should().Be(500);
        body.RootElement.GetProperty("title").GetString().Should().Be("Internal Server Error.");
        body.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
        // The server error does not travel in the envelope of the client errors.
        body.RootElement.TryGetProperty("error", out _).Should().BeFalse();
    }

    [Fact]
    public async Task TheForbiddenOfTheFrameworkCarriesTheErrorEnvelope()
    {
        SetAuth(role: "Employee");

        var response = await _client.PostAsJsonAsync("/api/Clients", new
        {
            name = "Employee Client",
            email = "employee@company.test",
            phone = "555-0098",
            address = "8 Y Street",
            imageUrl = (string?)null,
            isActive = true,
            createdAt = (DateTime?)null,
            updatedAt = (DateTime?)null,
            createdBy = (Guid?)null,
            updatedBy = (Guid?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        (await ErrorOfAsync(response)).Should().Be("Forbidden.");
    }

    [Fact]
    public async Task TheUnauthorizedOfTheFrameworkCarriesTheErrorEnvelope()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/suppliers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        (await ErrorOfAsync(response)).Should().Be("Unauthorized.");
    }

    private async Task<string?> ErrorOfAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return body.RootElement.TryGetProperty("error", out var error)
            ? error.GetString()
            : null;
    }

    private void SetAuth(string role = "Admin")
    {
        var token = JwtTestHelper.MintToken(_factory, Guid7TestValues.CompanyA, Guid7TestValues.UserA, role);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        db.Companies.Add(new Company
        {
            Id = Guid7TestValues.CompanyA,
            Name = "Company A",
            TaxId = "TA000000001",
            Address = "1 A Street",
            LegalName = "Company A Legal",
            Email = "a@company.test",
            Phone = "555-0001",
            IsActive = true
        });

        await db.SaveChangesAsync();
    }
}
