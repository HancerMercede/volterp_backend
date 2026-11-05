using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Volterp.Application.DTOs;
using Xunit;

namespace Volterp.Tests.Integration;

public class LoginAuthenticationTests : IClassFixture<ApiFactory>
{
    private const string SeededUsername = "admin";
    private const string SeededPassword = "Admin123!";
    private static readonly Guid SeededCompanyId = Guid.Parse("00000001-0000-0000-0000-000000000001");

    private readonly HttpClient _client;

    public LoginAuthenticationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureSchemaCreated();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = SeededUsername, password = SeededPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the credential lookup must not be tenant-scoped: before authenticating there is no company claim, so the tenant filter hides every row and login can never succeed.");

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrEmpty();
        body.Username.Should().Be(SeededUsername);
        body.CompanyId.Should().Be(SeededCompanyId);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = SeededUsername, password = "not-the-seeded-password" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "bypassing the tenant filter must not weaken the password check.");
    }
}
