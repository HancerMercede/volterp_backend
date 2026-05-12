using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Enums;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesController(IServiceManager serviceManager) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SaleDto>>> GetAllSales(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager
            .Sales
            .GetAllSalesAsync(companyId, pagination.PageNumber, pagination.PageSize, ct);
        
        return Ok(result);
    }
    
    [HttpGet("status/{status}")]
    public async Task<ActionResult<PagedResult<SaleDto>>> GetSalesByStatus(
        SaleStatus status,
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager
            .Sales
            .GetSalesByStatusAsync(companyId, status, pagination.PageNumber, pagination.PageSize, ct);
        
        return Ok(result);
    }
    
    [HttpGet("pending")]
    public async Task<ActionResult<PagedResult<SaleDto>>> GetPendingSales(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager
            .Sales
            .GetSalesByStatusAsync(companyId, SaleStatus.Pending, pagination.PageNumber, pagination.PageSize, ct);
        
        return Ok(result);
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<SaleDto>> GetSale(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var sale = await serviceManager
            .Sales
            .GetSaleByIdAsync(id, companyId, ct);
        
        if (sale is null)
            return NotFound(new { message = "Venta no encontrada" });
            
        return Ok(sale);
    }
    
    [HttpPost]
    public async Task<ActionResult<SaleDto>> CreateSale([FromBody] CreateSaleRequest request, CancellationToken ct = default)
    {
        try
        {
            var sale = await serviceManager
                .Sales
                .CreateSaleAsync(request, ct);
        
            return CreatedAtAction(nameof(GetSale), new { id = sale.Id }, sale);
        }
        catch (Exception e)
        {
            return BadRequest(new ErrorResponse("Could not create sale", e.Message));
        }
     
    }
    
    [HttpPut("{id}")]
    public async Task<ActionResult<SaleDto>> UpdateSale(int id, [FromBody] UpdateSaleRequest request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        
        try
        {
            var sale = await serviceManager.Sales.UpdateSaleAsync(id, companyId, request, ct);
            return Ok(sale);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
    
    [HttpPut("{id}/complete")]
    public async Task<ActionResult<SaleDto>> CompleteSale(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        
        try
        {
            var sale = await serviceManager.Sales.CompleteSaleAsync(id, companyId, ct);
            return Ok(sale);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
    
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteSale(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        
        try
        {
            await serviceManager.Sales.DeleteSaleAsync(id, companyId, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}