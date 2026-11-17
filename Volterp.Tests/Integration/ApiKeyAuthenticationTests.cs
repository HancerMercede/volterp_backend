using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Volterp.Api.Authentication;
using Volterp.Application.Common;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// The credential of a client that rents the API: the handler authenticates
// the machine and the tenant comes from the credential, not from a claim of
// a user.
[Collection("VolterpApiFactory")]
public class ApiKeyAuthenticationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    private static readonly object SeedLock = new();
    private static bool _seeded;

    public ApiKeyAuthenticationTests(ApiFactory factory)
    {
        _factory = factory;
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
    public async Task AValidKeyAuthenticatesAndStampsTheCompanyOfTheCredential()
    {
        var result = await AuthenticateAsync($"ApiKey {ValidKeyId}.{ValidSecret}");

        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst(Claims.CompanyId)!.Value
            .Should().Be(Guid7TestValues.CompanyA.ToString("N"));
        result.Principal.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task AMachineHasNoUser()
    {
        var result = await AuthenticateAsync($"ApiKey {ValidKeyId}.{ValidSecret}");

        result.Principal!.FindFirst(Claims.UserId).Should().BeNull("a machine is not a user of the ERP");
    }

    [Fact]
    public async Task AnUnknownKeyFailsClosed()
    {
        var result = await AuthenticateAsync("ApiKey UNKNOWNKEY01.not-the-secret");

        result.Succeeded.Should().BeFalse();
        result.Principal.Should().BeNull();
    }

    [Fact]
    public async Task AWrongSecretFailsClosed()
    {
        var result = await AuthenticateAsync($"ApiKey {ValidKeyId}.not-the-secret");

        result.Succeeded.Should().BeFalse();
        result.Principal.Should().BeNull();
    }

    [Fact]
    public async Task AKeyWithoutTheSeparatorFailsClosed()
    {
        var result = await AuthenticateAsync($"ApiKey {ValidKeyId}");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task ARevokedCredentialFailsClosed()
    {
        var result = await AuthenticateAsync($"ApiKey {RevokedKeyId}.{RevokedSecret}");

        result.Succeeded.Should().BeFalse();
        result.Principal.Should().BeNull();
    }

    [Fact]
    public async Task AnExpiredCredentialFailsClosed()
    {
        var result = await AuthenticateAsync($"ApiKey {ExpiredKeyId}.{ExpiredSecret}");

        result.Succeeded.Should().BeFalse();
        result.Principal.Should().BeNull();
    }

    [Fact]
    public async Task TheTenantIsTheOneOfTheCredentialAndNotTheOneOfTheRequest()
    {
        var result = await AuthenticateAsync(
            $"ApiKey {OtherTenantKeyId}.{OtherTenantSecret}",
            claimedCompanyId: Guid7TestValues.CompanyA);

        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst(Claims.CompanyId)!.Value
            .Should().Be(Guid7TestValues.CompanyB.ToString("N"),
                "the tenant of the call is the one of the credential");
    }

    [Fact]
    public void TheComparisonOfTheSecretIsInFixedTime()
    {
        // The time of the comparison cannot be observed from outside: the
        // property is pinned on the code that makes it.
        var source = SourceOf("Volterp.Api/Authentication/ApiKeyAuthenticationHandler.cs");

        source.Should().Contain("CryptographicOperations.FixedTimeEquals",
            "the secret of a client is not guessed one character at a time");
    }

    private async Task<AuthenticateResult> AuthenticateAsync(string authorization, Guid? claimedCompanyId = null)
    {
        using var scope = _factory.Services.CreateScope();

        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Headers.Authorization = new StringValues(authorization);

        if (claimedCompanyId is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(Claims.CompanyId, claimedCompanyId.Value.ToString("N"))]));
        }

        var handler = new ApiKeyAuthenticationHandler(
            new StaticOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            scope.ServiceProvider.GetRequiredService<IApiCredentialRepository>());

        await handler.InitializeAsync(
            new AuthenticationScheme(
                ApiKeyAuthenticationHandler.SchemeName, null, typeof(ApiKeyAuthenticationHandler)),
            context);

        return await handler.AuthenticateAsync();
    }

    private static string SourceOf(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Volterp.slnx")))
            directory = directory.Parent;

        return File.ReadAllText(Path.Combine(directory!.FullName, relativePath));
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        db.Set<ApiCredential>().AddRange(
            Credential(ValidKeyId, ValidSecret, Guid7TestValues.CompanyA),
            Credential(RevokedKeyId, RevokedSecret, Guid7TestValues.CompanyA, EntityStatus.Inactive),
            Credential(ExpiredKeyId, ExpiredSecret, Guid7TestValues.CompanyA, expiresAt: DateTime.UtcNow.AddMinutes(-1)),
            Credential(OtherTenantKeyId, OtherTenantSecret, Guid7TestValues.CompanyB));

        await db.SaveChangesAsync();
    }

    private static ApiCredential Credential(
        string keyId, string secret, Guid companyId, EntityStatus status = EntityStatus.Active, DateTime? expiresAt = null)
        => new()
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            Name = keyId,
            KeyId = keyId,
            SecretHash = ApiCredential.HashSecret(secret),
            Status = status,
            ExpiresAt = expiresAt
        };

    private const string ValidKeyId = "VALIDKEY001";
    private const string ValidSecret = "the-secret-of-the-valid-credential";
    private const string RevokedKeyId = "REVOKEDKEY1";
    private const string RevokedSecret = "the-secret-of-the-revoked-credential";
    private const string ExpiredKeyId = "EXPIREDKEY1";
    private const string ExpiredSecret = "the-secret-of-the-expired-credential";
    private const string OtherTenantKeyId = "OTHERTENANT";
    private const string OtherTenantSecret = "the-secret-of-the-credential-of-another-tenant";

    private sealed class StaticOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue => value;

        public TOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
    }
}
