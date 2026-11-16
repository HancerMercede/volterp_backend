using Volterp.Application.DTOs.CompanyDtos.EmissionMode;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompanyEmissionModeController(IServiceManager serviceManager) : BaseController
{
    private ICompanyEmissionModeService Service => serviceManager.CompanyEmissionModes;

    [HttpGet]
    public async Task<ActionResult<CompanyEmissionModeDto>> GetEmissionMode(CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await Service.GetAsync(companyId, ct);

        return result.Match<ActionResult<CompanyEmissionModeDto>>(
            error => NotFound(new ErrorResponse(error.Message)),
            emissionMode => Ok(emissionMode));
    }

    [HttpPut("contingency")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<CompanyEmissionModeDto>> UpdateContingency(
        [FromBody] UpdateCompanyContingencyDto request,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        var result = await Service.UpdateContingencyAsync(request, companyId, userId, ct);

        return result.Match<ActionResult<CompanyEmissionModeDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            emissionMode => Ok(emissionMode));
    }

    [HttpPut]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<CompanyEmissionModeDto>> UpdateEmissionMode(
        [FromBody] UpdateCompanyEmissionModeDto request,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        var result = await Service.UpdateAsync(request, companyId, userId, ct);

        return result.Match<ActionResult<CompanyEmissionModeDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            emissionMode => Ok(emissionMode));
    }
}
