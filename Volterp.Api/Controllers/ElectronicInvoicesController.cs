using System.Text;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ElectronicInvoicesController(IUnitOfWork unitOfWork) : BaseController
{
    [HttpGet("{id}/qr.svg")]
    public async Task<IActionResult> GetQrSvg(Guid id, CancellationToken ct = default)
    {
        var invoice = await FindAsync(id, ct);

        if (invoice is null)
            return NotFound(new ErrorResponse("The electronic invoice does not exist."));

        if (string.IsNullOrWhiteSpace(invoice.QrUrl))
            return NotFound(new ErrorResponse("The electronic invoice has no verification URL."));

        var svg = QrCodeRenderer.Svg(invoice.QrUrl);

        CacheTheImage();

        return File(Encoding.UTF8.GetBytes(svg), "image/svg+xml");
    }

    [HttpGet("{id}/qr.png")]
    public async Task<IActionResult> GetQrPng(Guid id, CancellationToken ct = default)
    {
        var invoice = await FindAsync(id, ct);

        if (invoice is null)
            return NotFound(new ErrorResponse("The electronic invoice does not exist."));

        if (string.IsNullOrWhiteSpace(invoice.QrUrl))
            return NotFound(new ErrorResponse("The electronic invoice has no verification URL."));

        var png = QrCodeRenderer.Png(invoice.QrUrl);

        CacheTheImage();

        return File(png, "image/png");
    }

    // The QR of a document never changes: the document is already signed and
    // its number is already taken, so the image can live in the browser.
    private void CacheTheImage()
        => Response.Headers.CacheControl = "private, max-age=86400, immutable";

    // The company travels by hand: the filter of the tenant already isolates
    // the document, and the check is what makes the isolation explicit at the
    // boundary.
    private async Task<ElectronicInvoice?> FindAsync(Guid id, CancellationToken ct)
    {
        var companyId = GetCurrentUserCompanyId();

        var invoice = await unitOfWork.ElectronicInvoices.GetByIdAsync(id, ct);

        return invoice is not null && invoice.CompanyId == companyId ? invoice : null;
    }
}
