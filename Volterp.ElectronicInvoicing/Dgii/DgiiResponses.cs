using System.Text.Json.Serialization;

namespace Volterp.ElectronicInvoicing.Dgii;

// The token the DGII issues after it validates our signed seed. It travels as
// the Authorization: bearer header on every later call.
public sealed record DgiiToken(string Token, DateTimeOffset? ExpiresAt, DateTimeOffset? IssuedAt)
{
    public bool IsExpired => IsExpiredAt(DateTimeOffset.UtcNow);

    public bool IsExpiredAt(DateTimeOffset now) => ExpiresAt is { } expiry && expiry <= now;
}

// The acknowledgement the DGII returns when it receives an e-CF: the track id
// is what lets us ask for the verdict afterwards.
public sealed record DgiiReceipt(string? TrackId, string? Error, string? Message)
{
    public bool IsRejected => !string.IsNullOrWhiteSpace(Error);
}

// One message the DGII attaches to a verdict: the only place it explains itself.
public sealed record DgiiMessage(string? Value, int Code);

// What the DGII answers when we ask by e-NCF whether it received the
// document: the track id it gave it, the state it is in and the day it
// received it. A track id that is not there means the DGII never got it,
// which is the answer that lets a lost send be sent again.
public sealed record DgiiTrackLookup(string? TrackId, string? Estado, DateTimeOffset? ReceivedAt)
{
    public bool NotFound => string.IsNullOrWhiteSpace(TrackId);
}

// The validation result of a received e-CF. SequenceConsumed is the one that
// bites: the DGII sets it when the rejection makes the number unusable.
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

internal sealed class DgiiTrackLookupResponse
{
    [JsonPropertyName("trackId")]
    public string? TrackId { get; set; }

    [JsonPropertyName("estado")]
    public string? Estado { get; set; }

    [JsonPropertyName("fechaRecepcion")]
    public string? FechaRecepcion { get; set; }
}
