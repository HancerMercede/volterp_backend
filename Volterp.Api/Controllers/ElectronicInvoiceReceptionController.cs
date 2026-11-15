using System.Text;
using Volterp.Application.DTOs.Reception;
using Volterp.Application.Services;

namespace Volterp.Api.Controllers;

[ApiController]
public class ElectronicInvoiceReceptionController(ElectronicInvoiceReceptionService service) : BaseController
{
    public const string DgiiAuthenticationScheme = "DgiiCertificate";

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

        var result = await service.ReceiveAsync(xml, file.FileName, ct);

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

        var result = await service.ReceiveCommercialApprovalAsync(xml, file.FileName, ct);

        return result.Match<ActionResult<ReceivedElectronicInvoiceDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            invoice => Ok(invoice));
    }

    [HttpPost("api/ElectronicInvoiceReception/{id}/commercial-approval")]
    [Authorize]
    public async Task<ActionResult<ReceivedElectronicInvoiceDto>> DecideCommercialApproval(
        Guid id,
        [FromBody] CommercialApprovalDecisionRequest request,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new ErrorResponse("The user id is required."));

        var result = await service.DecideCommercialApprovalAsync(
            id,
            companyId,
            userId.Value,
            request.Approved,
            request.Reason,
            ct);

        return result.Match<ActionResult<ReceivedElectronicInvoiceDto>>(
            error => error.Message == "The received e-CF does not exist."
                ? NotFound(new ErrorResponse(error.Message))
                : BadRequest(new ErrorResponse(error.Message)),
            invoice => Ok(invoice));
    }
}
