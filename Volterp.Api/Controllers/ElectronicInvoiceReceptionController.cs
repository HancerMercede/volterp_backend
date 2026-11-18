using System.Globalization;
using System.Text;
using System.Xml;
using Volterp.Application.DTOs.ReceivedElectronicInvoiceDtos;
using Volterp.Application.DTOs.Reception;
using Volterp.Application.DTOs.SeedDtos;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
public class ElectronicInvoiceReceptionController(
    IServiceManager serviceManager,
    ISeedAuthenticationService seedAuthentication) : BaseController
{
    public const string DgiiAuthenticationScheme = "DgiiCertificate";

    private readonly IElectronicInvoiceReceptionService _service = serviceManager.ElectronicInvoiceReceptions;

    [HttpPost("fe/recepcion/api/ecf")]
    [Consumes("multipart/form-data")]
    [Authorize(AuthenticationSchemes = DgiiAuthenticationScheme)]
    public async Task<ActionResult<ReceivedElectronicInvoiceDto>> Receive(
        [FromForm(Name = "xml")] IFormFile file,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ErrorResponse("The XML file is required."));

        using var reader = new StreamReader(
            file.OpenReadStream(),
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: false);
        var xml = await reader.ReadToEndAsync(ct);

        var result = await _service.ReceiveAsync(xml, file.FileName, ct);

        return result.Match<ActionResult<ReceivedElectronicInvoiceDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            invoice => Ok(invoice));
    }

    [HttpPost("fe/aprobacioncomercial/api/ecf")]
    [Consumes("multipart/form-data")]
    [Authorize(AuthenticationSchemes = DgiiAuthenticationScheme)]
    public async Task<ActionResult<ReceivedElectronicInvoiceDto>> ReceiveCommercialApproval(
        [FromForm(Name = "xml")] IFormFile file,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ErrorResponse("The XML file is required."));

        using var reader = new StreamReader(
            file.OpenReadStream(),
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: false);
        var xml = await reader.ReadToEndAsync(ct);

        var result = await _service.ReceiveCommercialApprovalAsync(xml, file.FileName, ct);

        return result.Match<ActionResult<ReceivedElectronicInvoiceDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            invoice => Ok(invoice));
    }

    // The seed of the family "Comunicación Emisor-Receptor" of the DGII: the
    // session the third party opens against this platform. It asks for it,
    // signs it and returns it signed to get a token.
    //
    // The route asks for no credential: the identity of the third party is
    // proven by the signature of the document it returns, not by anything it
    // presents here.
    [HttpGet("fe/autenticacion/api/semilla")]
    public async Task<IActionResult> Seed(CancellationToken ct = default)
    {
        var result = await seedAuthentication.IssueSeedAsync(ct);

        return result.Match<IActionResult>(
            error => BadRequest(new ErrorResponse(error.Message)),
            seed => File(SeedXml(seed), "text/xml"));
    }

    [HttpPost("fe/autenticacion/api/validacioncertificado")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<SeedValidationDto>> ValidateCertificate(
        [FromForm(Name = "xml")] IFormFile file,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ErrorResponse("The XML file is required."));

        using var reader = new StreamReader(
            file.OpenReadStream(),
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: false);
        var xml = await reader.ReadToEndAsync(ct);

        var result = await seedAuthentication.ValidateAsync(xml, ct);

        return result.Match<ActionResult<SeedValidationDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            validation => Ok(validation));
    }

    [HttpPost("api/ElectronicInvoiceReception/{id}/commercial-approval")]
    [Authorize]
    public async Task<ActionResult<ReceivedElectronicInvoiceDto?>> DecideCommercialApproval(
        Guid id,
        [FromBody] CommercialApprovalDecisionRequest request,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new ErrorResponse("The user id is required."));

        var result = await _service.DecideCommercialApprovalAsync(
            id,
            companyId,
            userId.Value,
            request.Approved,
            request.Reason,
            ct);

        // The document that does not exist is the absence of the answer: the
        // endpoint does not read the message of a failure to tell it apart.
        return result.Match<ActionResult<ReceivedElectronicInvoiceDto?>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            invoice => invoice is null
                ? NotFound(new ErrorResponse("The received e-CF does not exist."))
                : Ok(invoice));
    }

    // The shape of the answer is the one the DGII publishes (p. 44 of the
    // "Descripción Técnica Servicios DGII"): a SemillaModel with the value
    // and the moment it was generated. The output carries no indentation,
    // because the whitespace of the document is part of what gets digested
    // when the third party signs it.
    private static byte[] SeedXml(SeedDto seed)
    {
        var settings = new XmlWriterSettings
        {
            Indent = false,
            OmitXmlDeclaration = false,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            NewLineHandling = NewLineHandling.None
        };

        using var stream = new MemoryStream();

        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartElement("SemillaModel");
            writer.WriteAttributeString(
                "xmlns", "xsi", "http://www.w3.org/2000/xmlns/",
                "http://www.w3.org/2001/XMLSchema-instance");
            writer.WriteAttributeString(
                "xmlns", "xsd", "http://www.w3.org/2000/xmlns/",
                "http://www.w3.org/2001/XMLSchema");
            writer.WriteElementString("valor", seed.Valor);
            writer.WriteElementString("fecha", seed.Fecha.ToString("o", CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        return stream.ToArray();
    }
}
