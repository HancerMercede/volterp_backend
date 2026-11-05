using System.Text.Json;

namespace Volterp.Application.Credentials;

/// <summary>
/// The material an issuer signs its e-CF with: the PKCS#12 file and the
/// password that opens it.
/// </summary>
/// <remarks>
/// The DGII authenticates the web services with the signature of the
/// certificate itself: the seed is signed with the p12 and traded for a
/// token, with no user and no password in the protocol. So an issuer has ONE
/// secret, and the encrypted credential of the company certificate carries it
/// as an opaque envelope that only the ERP interprets.
/// </remarks>
public sealed record SigningCredential(string PfxBase64, string Password)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The opaque form that is stored as the encrypted credential.
    /// </summary>
    public string ToEnvelope() => JsonSerializer.Serialize(new Envelope(PfxBase64, Password), SerializerOptions);

    /// <summary>
    /// Reads the envelope back. A credential that cannot be read is a
    /// credential that will fail at signing time, and it fails here instead.
    /// </summary>
    public static SigningCredential Parse(string envelope)
    {
        if (!TryParse(envelope, out var credential, out var error))
            throw new InvalidOperationException(error);

        return credential;
    }

    /// <summary>
    /// Tells whether the envelope carries a usable signing material, and says
    /// why it does not when it does not.
    /// </summary>
    public static bool TryParse(string? envelope, out SigningCredential credential, out string? error)
    {
        credential = null!;
        error = null;

        if (string.IsNullOrWhiteSpace(envelope))
        {
            error = "The credential is empty.";
            return false;
        }

        Envelope? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<Envelope>(envelope, SerializerOptions);
        }
        catch (JsonException)
        {
            error = "The credential is not a signing material envelope.";
            return false;
        }

        if (parsed is null || string.IsNullOrWhiteSpace(parsed.Pfx) || string.IsNullOrEmpty(parsed.Password))
        {
            error = "The credential envelope must carry the pfx of the certificate and its password.";
            return false;
        }

        try
        {
            Convert.FromBase64String(parsed.Pfx);
        }
        catch (FormatException)
        {
            error = "The pfx of the credential is not valid base64.";
            return false;
        }

        credential = new SigningCredential(parsed.Pfx, parsed.Password);
        return true;
    }

    private sealed record Envelope(string? Pfx, string? Password);
}
