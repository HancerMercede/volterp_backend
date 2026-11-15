using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Volterp.DgiiSimulator;

/// <summary>
/// The taxpayer services of the DGII, as the "Descripción Técnica Servicios
/// DGII" describes them. Everything the simulator answers is written from that
/// document, not from the client that consumes it.
/// </summary>
internal static class SimulatorEndpoints
{
    // The RNC of the issuer (nine or eleven digits) followed by the e-NCF.
    private static readonly System.Text.RegularExpressions.Regex EcfFileNamePattern =
        new(@"^(?:\d{9}|\d{11})E\d{12}\.xml$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private const int SecurityCodeLength = 6;

    public static void Map(WebApplication application)
    {
        application.Use(async (context, next) =>
        {
            if (!DgiiPaths.HasExactCasing(context.Request.Path.Value))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next();
        });

        application.Use(SimulatorFaults.Apply);

        var group = application.MapGroup("/{environment}");

        group.MapGet(DgiiPaths.Seed, SeedAsync);
        group.MapPost(DgiiPaths.ValidateSeed, ValidateSeedAsync);
        group.MapPost(DgiiPaths.ReceiveElectronicInvoice, ReceiveAsync);
        group.MapGet(DgiiPaths.StatusByTrackId, StatusAsync);
        group.MapGet(DgiiPaths.TrackIdByEncf, TrackIdsAsync);
        group.MapGet(DgiiPaths.DocumentState, DocumentStateAsync);
    }

    private static async Task<IResult> SeedAsync(
        HttpContext context,
        DgiiSimulatorOptions options,
        SimulatorSeeds seeds)
    {
        await DelayAsync(context, options.SeedDelayMilliseconds);

        var value = seeds.Issue();
        return Results.Text(seeds.BuildXml(value), "application/xml", Encoding.UTF8);
    }

    private static async Task<IResult> ValidateSeedAsync(
        HttpContext context,
        DgiiSimulatorOptions options,
        SimulatorSeeds seeds,
        SimulatorTokens tokens)
    {
        await DelayAsync(context, options.SeedDelayMilliseconds);

        var file = await ReadXmlFileAsync(context);
        if (file is null)
            return Problem(StatusCodes.Status400BadRequest, "No se recibió el archivo XML firmado.");

        if (!SignedXmlVerifier.TryVerify(file.Content, out _, out _))
            return Problem(StatusCodes.Status400BadRequest, "El archivo XML no viene firmado.");

        // The seed is validated by the value it carries inside the signed
        // file: the document asks for no header and no envelope.
        if (!seeds.IsIssued(ExtractSeedValue(file.Content)))
            return Problem(StatusCodes.Status400BadRequest, "La semilla no fue emitida o ya venció.");

        var token = tokens.Issue(out var issuedAt, out var expiresAt);

        return Results.Json(new SeedTokenResponse(token, Format(expiresAt), Format(issuedAt)));
    }

    private static async Task<IResult> ReceiveAsync(
        HttpContext context,
        DgiiSimulatorOptions options,
        SimulatorTokens tokens,
        SimulatorDocuments documents)
    {
        await DelayAsync(context, options.ReceptionDelayMilliseconds);

        if (!tokens.TryValidate(ReadBearer(context), out var sessionId))
            return Unauthorized(context);

        var file = await ReadXmlFileAsync(context);
        if (file is null)
            return Problem(StatusCodes.Status400BadRequest, "No se recibió el archivo XML firmado.");

        if (!SignedXmlVerifier.TryVerify(file.Content, out _, out var signatureValue))
            return Problem(StatusCodes.Status400BadRequest, "El archivo XML no viene firmado.");

        if (!IsEcfFileName(file.FileName))
            return Problem(
                StatusCodes.Status400BadRequest,
                $"El nombre del archivo debe ser RNC+e-NCF: {file.FileName}");

        EcfData data;
        try
        {
            data = EcfDocumentReader.Read(file.Content);
        }
        catch (XmlException)
        {
            return Problem(StatusCodes.Status400BadRequest, "El archivo XML no es un comprobante válido.");
        }

        if (string.IsNullOrWhiteSpace(data.IssuerRnc) || string.IsNullOrWhiteSpace(data.Encf))
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "El comprobante no dice el RNC emisor ni el e-NCF.");
        }

        if (options.AllowedIssuerRnc is { Length: > 0 } allowed
            && !string.Equals(allowed, data.IssuerRnc, StringComparison.Ordinal))
        {
            return Problem(StatusCodes.Status400BadRequest, "El RNC emisor no está autorizado en este simulador.");
        }

        var verdict = options.DefaultVerdict;
        if (context.Request.Headers.ContainsKey(SimulatorControl.VerdictHeader))
        {
            if (!TryReadVerdict(context, out var requested))
                return Problem(StatusCodes.Status400BadRequest, "El veredicto solicitado no existe.");

            verdict = requested;
        }

        var receivedAt = DateTimeOffset.UtcNow;
        var document = documents.Add(new SimulatorDocument
        {
            TrackId = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            IssuerRnc = data.IssuerRnc,
            Encf = data.Encf,
            BuyerRnc = data.BuyerRnc,
            ForeignId = data.ForeignId,
            TotalAmount = data.TotalAmount,
            TotalItbis = data.TotalItbis,
            IssueDate = data.IssueDate,
            SignedAt = data.SignedAt ?? Format(receivedAt),
            SecurityCode = SecurityCode(signatureValue),
            ReceivedAt = receivedAt,
            Verdict = verdict,
            VerdictAt = verdict == SimulatorVerdict.InProcess && options.InProcessDurationMilliseconds > 0
                ? receivedAt.AddMilliseconds(options.InProcessDurationMilliseconds)
                : null,
            FinalVerdict = options.VerdictAfterProcessing
        });

        return Results.Json(new ReceptionResponse(document.TrackId, null, options.AcceptedMessage));
    }

    private static async Task<IResult> StatusAsync(
        HttpContext context,
        DgiiSimulatorOptions options,
        SimulatorTokens tokens,
        SimulatorDocuments documents)
    {
        await DelayAsync(context, options.QueryDelayMilliseconds);

        if (!tokens.TryValidate(ReadBearer(context), out _))
            return Unauthorized(context);

        var trackId = context.Request.Query["trackid"].ToString();
        var document = documents.FindByTrackId(trackId);

        if (document is null)
        {
            return Results.Json(new StatusResponse(
                trackId,
                (int)SimulatorVerdict.NotFound,
                Estado(SimulatorVerdict.NotFound),
                null,
                null,
                false,
                null,
                []));
        }

        var verdict = document.CurrentVerdict(DateTimeOffset.UtcNow);
        var mensajes = verdict == SimulatorVerdict.Rejected
            ? new[] { new SimulatorMessageResponse(options.RejectionMessage, options.RejectionCode) }
            : [];

        return Results.Json(new StatusResponse(
            document.TrackId,
            (int)verdict,
            Estado(verdict),
            document.IssuerRnc,
            document.Encf,
            SequenceIsConsumed(verdict),
            Format(document.ReceivedAt),
            mensajes));
    }

    private static async Task<IResult> TrackIdsAsync(
        HttpContext context,
        DgiiSimulatorOptions options,
        SimulatorTokens tokens,
        SimulatorDocuments documents)
    {
        await DelayAsync(context, options.QueryDelayMilliseconds);

        if (!tokens.TryValidate(ReadBearer(context), out var sessionId))
            return Unauthorized(context);

        var document = documents.FindByEncf(
            sessionId,
            context.Request.Query["rncemisor"].ToString(),
            context.Request.Query["encf"].ToString());

        if (document is null)
            return Results.Json(new TrackLookupResponse(string.Empty, Estado(SimulatorVerdict.NotFound), null));

        var verdict = document.CurrentVerdict(DateTimeOffset.UtcNow);

        return Results.Json(new TrackLookupResponse(
            document.TrackId,
            Estado(verdict),
            Format(document.ReceivedAt)));
    }

    private static async Task<IResult> DocumentStateAsync(
        HttpContext context,
        DgiiSimulatorOptions options,
        SimulatorTokens tokens,
        SimulatorDocuments documents)
    {
        await DelayAsync(context, options.QueryDelayMilliseconds);

        if (!tokens.TryValidate(ReadBearer(context), out var sessionId))
            return Unauthorized(context);

        var issuerRnc = context.Request.Query["rncemisor"].ToString();
        var encf = context.Request.Query["ncfelectronico"].ToString();
        var document = documents.FindByEncf(sessionId, issuerRnc, encf);

        if (document is null)
        {
            return Results.Json(new DocumentStateResponse(
                (int)SimulatorVerdict.NotFound,
                Estado(SimulatorVerdict.NotFound),
                issuerRnc,
                encf,
                null,
                null,
                null,
                null,
                null,
                null,
                null));
        }

        var verdict = document.CurrentVerdict(DateTimeOffset.UtcNow);

        return Results.Json(new DocumentStateResponse(
            (int)verdict,
            Estado(verdict),
            document.IssuerRnc,
            document.Encf,
            document.TotalAmount,
            document.TotalItbis,
            document.IssueDate,
            document.SignedAt,
            document.BuyerRnc,
            document.SecurityCode,
            document.ForeignId));
    }

    private static string? ReadBearer(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header))
            return null;

        var parts = header.Split(' ', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return null;

        if (!parts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase))
            return null;

        return parts[1];
    }

    private static async Task<XmlFile?> ReadXmlFileAsync(HttpContext context)
    {
        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(context.RequestAborted);
        }
        catch (InvalidDataException)
        {
            return null;
        }

        var file = form.Files["xml"];
        if (file is null || file.Length == 0)
            return null;

        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync(context.RequestAborted);

        return new XmlFile(content, file.FileName);
    }

    private sealed record XmlFile(string Content, string FileName);

    private static string? ExtractSeedValue(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml);
            return document.Descendants().FirstOrDefault(element => element.Name.LocalName == "valor")?.Value;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// The name of a document is part of the contract: the RNC of the issuer
    /// followed by the e-NCF (p. 13 of the "Descripción Técnica Servicios
    /// DGII").
    /// </summary>
    private static bool IsEcfFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        return EcfFileNamePattern.IsMatch(fileName.Trim());
    }

    private static bool TryReadVerdict(HttpContext context, out SimulatorVerdict verdict)
    {
        var raw = context.Request.Headers[SimulatorControl.VerdictHeader].ToString().Trim().ToLowerInvariant();

        switch (raw)
        {
            case "accepted":
                verdict = SimulatorVerdict.Accepted;
                return true;
            case "rejected":
                verdict = SimulatorVerdict.Rejected;
                return true;
            case "in-process":
            case "inprocess":
                verdict = SimulatorVerdict.InProcess;
                return true;
            case "accepted-conditionally":
            case "acceptedconditionally":
                verdict = SimulatorVerdict.AcceptedConditionally;
                return true;
            default:
                verdict = SimulatorVerdict.Accepted;
                return false;
        }
    }

    /// <summary>
    /// The security code is the first six characters of the signature value
    /// (p. 28 of the "Descripción Técnica Servicios DGII"). They are taken as
    /// they are, without leaving out any character, so the code is the same
    /// one the taxpayer prints next to the QR.
    /// </summary>
    private static string? SecurityCode(string? signatureValue)
    {
        if (string.IsNullOrWhiteSpace(signatureValue) || signatureValue.Length < SecurityCodeLength)
            return null;

        return signatureValue[..SecurityCodeLength];
    }

    /// <summary>
    /// The flag says whether the number of the sequence can be used again:
    /// true means it cannot (p. 25 of the "Descripción Técnica Servicios
    /// DGII"). A document that is valid took the number, and a document that
    /// was rejected left it free, which is the scenario the document
    /// describes.
    /// </summary>
    private static bool SequenceIsConsumed(SimulatorVerdict verdict) =>
        verdict is SimulatorVerdict.Accepted or SimulatorVerdict.AcceptedConditionally;

    private static string Estado(SimulatorVerdict verdict) => verdict switch
    {
        SimulatorVerdict.Accepted => "Aceptado",
        SimulatorVerdict.Rejected => "Rechazado",
        SimulatorVerdict.InProcess => "En Proceso",
        SimulatorVerdict.AcceptedConditionally => "Aceptado Condicional",
        _ => "No encontrado"
    };

    private static string Format(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static async Task DelayAsync(HttpContext context, int milliseconds)
    {
        if (milliseconds > 0)
            await Task.Delay(milliseconds, context.RequestAborted);
    }

    private static IResult Problem(int statusCode, string detail) =>
        Results.Problem(statusCode: statusCode, detail: detail);

    private static IResult Unauthorized(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            detail: "El token no es válido o venció.");
    }
}
