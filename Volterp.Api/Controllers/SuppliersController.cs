using Volterp.Application.DTOs.SupplierDtos;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SuppliersController(IServiceManager serviceManager) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SupplierDto>>> GetAllSuppliers(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager
            .Suppliers
            .GetAllSuppliersAsync(companyId, pagination.PageNumber, pagination.PageSize, ct);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SupplierDto>> GetSupplier(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager
            .Suppliers
            .GetSupplierByIdAsync(id, companyId, ct);

        return result.Match<ActionResult<SupplierDto>>(
            error => NotFound(new { message = error.Message }),
            value => Ok(value));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<SupplierDto>> CreateSupplier(
        [FromBody] SupplierCreateDto request,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager
            .Suppliers
            .CreateSupplierAsync(request, companyId, ct);

        return result.Match<ActionResult<SupplierDto>>(
            error => BadRequest(new { message = error.Message }),
            value => CreatedAtAction(nameof(GetSupplier), new { id = value.Id }, value));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<SupplierDto>> UpdateSupplier(
        int id,
        [FromBody] SupplierDto request,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager
            .Suppliers
            .UpdateSupplierAsync(id, companyId, request, ct);

        return result.Match<ActionResult<SupplierDto>>(
            error => NotFound(new { message = error.Message }),
            value => Ok(value));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> DeleteSupplier(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager
            .Suppliers
            .DeleteSupplierAsync(id, companyId, ct);

        return result.Match<IActionResult>(
            error => NotFound(new { message = error.Message }),
            _ => NoContent());
    }
}
