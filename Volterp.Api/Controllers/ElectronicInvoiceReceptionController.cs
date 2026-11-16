using System.Text;
using Volterp.Application.DTOs.Reception;
using Volterp.Application.DTOs.ReceivedElectronicInvoiceDtos;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
public class ElectronicInvoiceReceptionController(IServiceManager serviceManager) : BaseController
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
}
