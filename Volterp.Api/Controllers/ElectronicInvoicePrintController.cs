using System.Text;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ElectronicInvoicePrintDtos;
using Volterp.Application.Interfaces;
using Volterp.ElectronicInvoicing.Printing;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ElectronicInvoicePrintController(IServiceManager serviceManager) : BaseController
{
    // The printed representation of an e-CF: the HTML the browser prints.
    // The PDF is the one the browser writes when it prints the page.
    private readonly IElectronicInvoicePrintService _print = serviceManager.ElectronicInvoicePrints;

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();

        var result = await _print.PrintAsync(id, companyId, ct);

        return result.Match<IActionResult>(
            error => BadRequest(new ErrorResponse(error.Message)),
            print => File(Encoding.UTF8.GetBytes(Html(print)), "text/html; charset=utf-8"));
    }

    // The QR is drawn here, where the library that draws it lives: the
    // representation carries the image of that QR, so the paper does not
    // depend on the browser reaching the server to draw it.
    private static string Html(ElectronicInvoicePrintDto print)
    {
        var svg = QrCodeRenderer.Svg(print.QrUrl);

        return EcfPrintRenderer.Render(new EcfPrintRequest
        {
            Document = print.Document,
            AdditionalTaxes = print.AdditionalTaxes,
            Pagination = print.Pagination,
            ItemAmountsIncludeItbis = print.ItemAmountsIncludeItbis,
            SecurityCode = print.SecurityCode,
            QrUrl = print.QrUrl,
            QrImageSvg = svg
        });
    }
}
