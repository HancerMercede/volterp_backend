using Volterp.Application.DTOs.NormalFiscalSequenceDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NormalFiscalSequencesController(IServiceManager serviceManager) : BaseController
{
    private INormalFiscalSequenceService Service => serviceManager.NormalFiscalSequences;

    [HttpGet]
    public async Task<ActionResult<PagedResult<NormalFiscalSequenceDto>>> GetAllSequences(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await Service.GetAllAsync(companyId, pagination.PageNumber, pagination.PageSize, ct);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<NormalFiscalSequenceDto>> GetSequence(Guid id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await Service.GetByIdAsync(id, companyId, ct);

        return result.Match<ActionResult<NormalFiscalSequenceDto>>(
            error => NotFound(new ErrorResponse(error.Message)),
            range => Ok(range)
        );
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<NormalFiscalSequenceDto>> CreateSequence(
        [FromBody] SaveNormalFiscalSequenceDto request,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await Service.CreateAsync(request, companyId, ct);

        return result.Match<ActionResult<NormalFiscalSequenceDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            range => CreatedAtAction(nameof(GetSequence), new { id = range.Id }, range)
        );
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<NormalFiscalSequenceDto>> UpdateSequence(
        Guid id,
        [FromBody] SaveNormalFiscalSequenceDto request,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await Service.UpdateAsync(id, request, companyId, ct);

        return result.Match<ActionResult<NormalFiscalSequenceDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            range => Ok(range)
        );
    }
}
