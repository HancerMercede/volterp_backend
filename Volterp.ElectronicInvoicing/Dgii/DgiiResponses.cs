using System.Text.Json.Serialization;

namespace Volterp.ElectronicInvoicing.Dgii;

/// <summary>
/// The token the DGII issues after it validates our signed seed. It travels as
/// the <c>Authorization: bearer</c> header on every subsequent call.
/// </summary>
public sealed record DgiiToken(string Token, DateTimeOffset? ExpiresAt, DateTimeOffset? IssuedAt)
{
    /// <summary>
    /// Whether the token is already past its expiration. The DGII reports the
    /// expiration date and a token past it stops working.
    /// </summary>
    public bool IsExpired => ExpiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow;
}

/// <summary>
/// The acknowledgement the DGII returns when it receives an e-CF: the track id
/// is what lets us ask for the verdict afterwards.
/// </summary>
public sealed record DgiiReceipt(string? TrackId, string? Error, string? Message)
{
    /// <summary>
    /// Whether the DGII refused the file outright. When it does, there is no
    /// track id to poll and the error explains why.
    /// </summary>
    public bool IsRejected => !string.IsNullOrWhiteSpace(Error);
}

/// <summary>
/// One message the DGII attaches to a verdict. These carry the reason the e-CF
/// was rejected or conditionally accepted, and they are the only place the DGII
/// explains itself.
/// </summary>
public sealed record DgiiMessage(string? Value, int Code);

/// <summary>
/// The validation result of a received e-CF.
/// </summary>
/// <param name="TrackId">The track id the verdict belongs to.</param>
/// <param name="Verdict">The DGII validation code.</param>
/// <param name="StatusText">The verdict as the DGII words it.</param>
/// <param name="Rnc">The RNC of the emitter that sent the e-CF.</param>
/// <param name="Encf">The e-NCF the verdict refers to.</param>
/// <param name="SequenceConsumed">
/// True when the sequence cannot be reused. The DGII sets it when the e-CF was
/// rejected for reasons that make the number unusable: an invalid signature or
/// structure, an unauthorized or expired e-NCF, or an emitter that is not
/// registered or active.
/// </param>
/// <param name="ReceivedAt">When the DGII received the e-CF.</param>
/// <param name="Messages">The reasons behind the verdict.</param>
public sealed record DgiiStatus(
    string? TrackId,
    DgiiVerdict Verdict,
    string? StatusText,
    string? Rnc,
    string? Encf,
    bool SequenceConsumed,
    DateTimeOffset? ReceivedAt,
    IReadOnlyList<DgiiMessage> Messages);

internal sealed class DgiiTokenResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("expira")]
    public string? Expira { get; set; }

    [JsonPropertyName("expedido")]
    public string? Expedido { get; set; }
}

internal sealed class DgiiReceiptResponse
{
    [JsonPropertyName("trackId")]
    public string? TrackId { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("mensaje")]
    public string? Message { get; set; }
}

internal sealed class DgiiStatusResponse
{
    [JsonPropertyName("trackId")]
    public string? TrackId { get; set; }

    [JsonPropertyName("codigo")]
    public int Codigo { get; set; }

    [JsonPropertyName("estado")]
    public string? Estado { get; set; }

    [JsonPropertyName("rnc")]
    public string? Rnc { get; set; }

    [JsonPropertyName("eNCF")]
    public string? Encf { get; set; }

    [JsonPropertyName("secuenciaUtilizada")]
    public bool SecuenciaUtilizada { get; set; }

    [JsonPropertyName("fechaRecepcion")]
    public string? FechaRecepcion { get; set; }

    [JsonPropertyName("mensajes")]
    public List<DgiiMessageResponse>? Mensajes { get; set; }
}

internal sealed class DgiiMessageResponse
{
    [JsonPropertyName("valor")]
    public string? Value { get; set; }

    [JsonPropertyName("codigo")]
    public int Code { get; set; }
}
