using System.Text.Json.Serialization;

namespace Volterp.DgiiSimulator;

/// <summary>
/// The answers of the DGII, with the names the "Descripción Técnica Servicios
/// DGII" gives them.
/// </summary>
internal sealed record SeedTokenResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("expira")] string Expira,
    [property: JsonPropertyName("expedido")] string Expedido);

internal sealed record ReceptionResponse(
    [property: JsonPropertyName("trackId")] string TrackId,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("mensaje")] string Mensaje);

internal sealed record StatusResponse(
    [property: JsonPropertyName("trackId")] string TrackId,
    [property: JsonPropertyName("codigo")] int Codigo,
    [property: JsonPropertyName("estado")] string Estado,
    [property: JsonPropertyName("rnc")] string? Rnc,
    [property: JsonPropertyName("eNCF")] string? Encf,
    [property: JsonPropertyName("secuenciaUtilizada")] bool SecuenciaUtilizada,
    [property: JsonPropertyName("fechaRecepcion")] string? FechaRecepcion,
    [property: JsonPropertyName("mensajes")] IReadOnlyList<SimulatorMessageResponse> Mensajes);

internal sealed record SimulatorMessageResponse(
    [property: JsonPropertyName("valor")] string Valor,
    [property: JsonPropertyName("codigo")] int Codigo);

internal sealed record TrackLookupResponse(
    [property: JsonPropertyName("trackId")] string TrackId,
    [property: JsonPropertyName("estado")] string Estado,
    [property: JsonPropertyName("fechaRecepcion")] string? FechaRecepcion);

internal sealed record DocumentStateResponse(
    [property: JsonPropertyName("codigo")] int Codigo,
    [property: JsonPropertyName("estado")] string Estado,
    [property: JsonPropertyName("rncEmisor")] string? RncEmisor,
    [property: JsonPropertyName("ncfElectronico")] string? NcfElectronico,
    [property: JsonPropertyName("montoTotal")] decimal? MontoTotal,
    [property: JsonPropertyName("totalITBIS")] decimal? TotalItbis,
    [property: JsonPropertyName("fechaEmision")] string? FechaEmision,
    [property: JsonPropertyName("fechaFirma")] string? FechaFirma,
    [property: JsonPropertyName("rncComprador")] string? RncComprador,
    [property: JsonPropertyName("codigoSeguridad")] string? CodigoSeguridad,
    [property: JsonPropertyName("idExtranjero")] string? IdExtranjero);
