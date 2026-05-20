using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;

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
        var supplier = await serviceManager
            .Suppliers
            .GetSupplierByIdAsync(id, companyId, ct);
        
        if (supplier is null)
            return NotFound(new { message = "Proveedor no encontrado" });
            
        return Ok(supplier);
    }
    
    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<SupplierDto>> CreateSupplier([FromBody] CreateSupplierRequest request, CancellationToken ct = default)
    {
        try
        {
            var companyId = GetCurrentUserCompanyId();
            var supplier = await serviceManager
                .Suppliers
                .CreateSupplierAsync(request, companyId, ct);
        
            return CreatedAtAction(nameof(GetSupplier), new { id = supplier.Id }, supplier);
        }
        catch (Exception e)
        {
            return BadRequest(new ErrorResponse("Could not create supplier", e.Message));
        }
    }
    
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<SupplierDto>> UpdateSupplier(int id, [FromBody] UpdateSupplierRequest request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        
        try
        {
            var supplier = await serviceManager.Suppliers.UpdateSupplierAsync(id, companyId, request, ct);
            return Ok(supplier);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
    
    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult> DeleteSupplier(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        
        try
        {
            await serviceManager.Suppliers.DeleteSupplierAsync(id, companyId, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}