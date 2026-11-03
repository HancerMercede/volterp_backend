using Volterp.Application.DTOs.CompanyCertificateDtos;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompanyCertificatesController(IServiceManager serviceManager) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CompanyCertificateDto>>> GetAllCertificates(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        EnsureTenant();
        var result = await serviceManager
            .CompanyCertificates
            .GetAllAsync(pagination.PageNumber, pagination.PageSize, ct);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CompanyCertificateDto>> GetCertificate(Guid id, CancellationToken ct = default)
    {
        EnsureTenant();
        var result = await serviceManager.CompanyCertificates.GetByIdAsync(id, ct);
        return result.Match<ActionResult<CompanyCertificateDto>>(
            error => NotFound(new ErrorResponse(error.Message)),
            certificate => Ok(certificate)
        );
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<CompanyCertificateDto>> CreateCertificate(
        [FromBody] CreateCompanyCertificateDto request,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId() ?? Guid.Empty;
        var result = await serviceManager.CompanyCertificates.CreateAsync(request, companyId, userId, ct);
        return result.Match<ActionResult<CompanyCertificateDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            certificate => CreatedAtAction(nameof(GetCertificate), new { id = certificate.Id }, certificate)
        );
    }
}
