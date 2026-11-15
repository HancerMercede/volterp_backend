using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace Volterp.DgiiSimulator;

/// <summary>
/// The token the simulator issues once it accepts a signed seed. It travels as
/// the authorization header of every later call, and it says which session
/// issued it: the delegation the DGII asks for before a taxpayer may see
/// somebody else's document is approximated by that session.
/// </summary>
internal sealed class SimulatorTokens(X509Certificate2 certificate, DgiiSimulatorOptions options)
{
    private const string Issuer = "volterp-dgii-simulator";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string Issue(out DateTimeOffset issuedAt, out DateTimeOffset expiresAt)
    {
        issuedAt = DateTimeOffset.UtcNow;
        expiresAt = issuedAt.AddMinutes(options.TokenLifetimeMinutes);

        var header = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var payload = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            sid = Guid.NewGuid().ToString("N"),
            iss = Issuer,
            iat = issuedAt.ToUnixTimeSeconds(),
            exp = expiresAt.ToUnixTimeSeconds()
        }));

        var signed = Encoding.UTF8.GetBytes($"{header}.{payload}");
        using var rsa = certificate.GetRSAPrivateKey()
                        ?? throw new InvalidOperationException("The simulator certificate has no private key.");
        var signature = rsa.SignData(signed, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{header}.{payload}.{Base64Url.Encode(signature)}";
    }

    /// <summary>
    /// A token is good when it carries the simulator's own signature and has
    /// not expired. Anything else is refused: the simulator never hands a
    /// session to a caller it cannot identify.
    /// </summary>
    public bool TryValidate(string? token, out string sessionId)
    {
        sessionId = string.Empty;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        var parts = token.Split('.');
        if (parts.Length != 3)
            return false;

        byte[] signature;
        try
        {
            signature = Base64Url.Decode(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        using var rsa = certificate.GetRSAPublicKey();
        if (rsa is null)
            return false;

        var signed = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
        if (!rsa.VerifyData(signed, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            return false;

        TokenPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<TokenPayload>(Base64Url.Decode(parts[1]), Json);
        }
        catch (JsonException)
        {
            return false;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Sid))
            return false;

        if (payload.Exp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            return false;

        sessionId = payload.Sid;
        return true;
    }

    private sealed class TokenPayload
    {
        public string? Sid { get; set; }
        public long Exp { get; set; }
    }
}
