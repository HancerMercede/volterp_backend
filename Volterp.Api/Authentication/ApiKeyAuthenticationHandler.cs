using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Volterp.Application.Common;
using Volterp.Application.DTOs;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Api.Authentication;

// The credential of a client that rents the API. The tenant comes from the
// credential and not from a claim of a user: a machine has no user.
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiCredentialRepository credentials)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderPrefix = SchemeName + " ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();

        if (!header.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.Fail("The request does not carry an API key.");

        var presented = header[HeaderPrefix.Length..].Trim();
        var separator = presented.IndexOf('.');

        if (separator <= 0 || separator == presented.Length - 1)
            return AuthenticateResult.Fail("The API key is malformed.");

        var keyId = presented[..separator];
        var secret = presented[(separator + 1)..];

        var credential = await credentials.FindByKeyIdAsync(keyId, Context.RequestAborted);

        if (credential is null)
            return AuthenticateResult.Fail("The API key does not exist.");

        if (credential.Status != EntityStatus.Active)
            return AuthenticateResult.Fail("The API key is not active.");

        if (credential.IsExpired)
            return AuthenticateResult.Fail("The API key has expired.");

        // The comparison is in fixed time: the secret of a client is not
        // guessed one character at a time.
        var presentedHash = ApiCredential.HashSecret(secret);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(presentedHash),
                Encoding.UTF8.GetBytes(credential.SecretHash)))
        {
            return AuthenticateResult.Fail("The API key is not valid.");
        }

        var identity = new ClaimsIdentity(
            [new Claim(Claims.CompanyId, credential.CompanyId.ToString("N"))],
            Scheme.Name);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // The answer of a rejected key travels in the same envelope every
        // other error of the API uses.
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        Response.Headers.Append("WWW-Authenticate", SchemeName);

        await Response.WriteAsync(
            JsonSerializer.Serialize(new ErrorResponse("Unauthorized."), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        // A machine that reaches something it may not do gets the same
        // answer a user gets.
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/json";

        await Response.WriteAsync(
            JsonSerializer.Serialize(new ErrorResponse("Forbidden."), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
