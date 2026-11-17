using Microsoft.AspNetCore.Authorization;
using Volterp.Application.DTOs.ApiCredentialDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ApiCredentialsController(IApiCredentialService service) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ApiCredentialDto>>> GetAllCredentials(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await service.GetAllAsync(companyId, pagination.PageNumber, pagination.PageSize, ct);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<IssuedApiCredentialDto>> CreateCredential(
        [FromBody] CreateApiCredentialDto request,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        var result = await service.CreateAsync(request, companyId, userId, ct);

        return result.Match<ActionResult<IssuedApiCredentialDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            issued => Ok(issued));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult> RevokeCredential(Guid id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await service.RevokeAsync(id, companyId, ct);

        return result.Match<ActionResult>(
            error => error.Kind == ErrorKind.NotFound
                ? NotFound(new ErrorResponse(error.Message))
                : BadRequest(new ErrorResponse(error.Message)),
            credential => NoContent());
    }
}
