using Volterp.Application.DTOs.CompanyIssuerDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

// The issuer RNCs of the tenant: the branches that issue on its behalf. The
// company from the token owns them, never the body.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompanyIssuersController(IServiceManager serviceManager) : BaseController
{
    private ICompanyIssuerService Service => serviceManager.CompanyIssuers;

    [HttpGet]
    public async Task<ActionResult<PagedResult<CompanyIssuerDto>>> GetAllIssuers(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await Service.GetAllAsync(companyId, pagination.PageNumber, pagination.PageSize, ct);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CompanyIssuerDto>> GetIssuer(Guid id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await Service.GetByIdAsync(id, companyId, ct);

        return result.Match<ActionResult<CompanyIssuerDto>>(
            error => NotFound(new ErrorResponse(error.Message)),
            issuer => Ok(issuer)
        );
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<CompanyIssuerDto>> CreateIssuer(
        [FromBody] CreateCompanyIssuerRequest request,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await Service.CreateAsync(request, companyId, GetCurrentUserId(), ct);

        return result.Match<ActionResult<CompanyIssuerDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            issuer => CreatedAtAction(nameof(GetIssuer), new { id = issuer.Id }, issuer)
        );
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<CompanyIssuerDto>> UpdateIssuer(
        Guid id,
        [FromBody] UpdateCompanyIssuerRequest request,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await Service.UpdateAsync(id, request, companyId, GetCurrentUserId(), ct);

        return result.Match<ActionResult<CompanyIssuerDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            issuer => Ok(issuer)
        );
    }
}
