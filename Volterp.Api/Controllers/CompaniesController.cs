using Volterp.Application.DTOs.CompanyDtos;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompaniesController(IServiceManager serviceManager, ILogger<CompaniesController> logger):BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CompanyDto>>> GetAllCompanies(
        [FromQuery] PaginationParameters parameters,
        CancellationToken ct = default)
    {
        if (!IsAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

        var companies = await serviceManager.Companies.GetAllCompaniesAsync(
            parameters.PageNumber,
            parameters.PageSize,
            ct);

        return Ok(companies);
    }

    [HttpGet("{id}", Name = "GetCompany")]
    public async Task<ActionResult<CompanyDto>> GetCompany(Guid id, CancellationToken ct = default)
    {
        if (!IsAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

        var company = await serviceManager.Companies.GetCompanyByIdAsync(id, ct);

        return company.Match<ActionResult<CompanyDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            result=>Ok(result));

    }

    [HttpPost]
    public async Task<ActionResult<CompanyDto>> CreateCompany([FromBody] CreateCompanyDto createCompanyDto,
        CancellationToken ct = default)
    {
        if (!IsAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

        var userId = GetCurrentUserId();
        var company = await serviceManager.Companies.AddCompanyAsync(createCompanyDto, userId, ct);

        return company.Match<ActionResult<CompanyDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            _ => Created("GetCompany", company));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateCompany(Guid id, [FromBody] UpdateCompanyDto updateCompanyDto,
        CancellationToken ct = default)
    {
        if(!IsAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));
        var userId = GetCurrentUserId();
        var company = await serviceManager.Companies.UpdateCompanyAsync(id, updateCompanyDto, userId, ct);

        return company.Match<ActionResult>(
            error => BadRequest(new ErrorResponse(error.Message)),
            result => Ok(result));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteCompany(Guid id, CancellationToken ct = default)
    {
        if(!IsAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));
        await serviceManager.Companies.DeleteCompanyAsync(id, ct);
        return NoContent();
    }
}