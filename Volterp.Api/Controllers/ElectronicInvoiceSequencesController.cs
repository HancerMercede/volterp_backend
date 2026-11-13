using Volterp.Application.DTOs.ElectronicInvoiceSequenceDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ElectronicInvoiceSequencesController(IServiceManager serviceManager) : BaseController
{
    private IElectronicInvoiceSequenceService Service => serviceManager.ElectronicInvoiceSequences;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ElectronicInvoiceSequenceDto>>> GetAllSequences(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await Service.GetAllAsync(companyId, pagination.PageNumber, pagination.PageSize, ct);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ElectronicInvoiceSequenceDto>> GetSequence(Guid id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await Service.GetByIdAsync(id, companyId, ct);

        return result.Match<ActionResult<ElectronicInvoiceSequenceDto>>(
            error => NotFound(new ErrorResponse(error.Message)),
            range => Ok(range)
        );
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ElectronicInvoiceSequenceDto>> CreateSequence(
        [FromBody] SaveElectronicInvoiceSequenceDto request,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await Service.CreateAsync(request, companyId, ct);

        return result.Match<ActionResult<ElectronicInvoiceSequenceDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            range => CreatedAtAction(nameof(GetSequence), new { id = range.Id }, range)
        );
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ElectronicInvoiceSequenceDto>> UpdateSequence(
        Guid id,
        [FromBody] SaveElectronicInvoiceSequenceDto request,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await Service.UpdateAsync(id, request, companyId, ct);

        return result.Match<ActionResult<ElectronicInvoiceSequenceDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            range => Ok(range)
        );
    }
}
