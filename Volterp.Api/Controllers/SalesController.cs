using EitherWay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Enums;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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
        var result = await serviceManager.Sales.GetSaleByIdAsync(id, companyId, ct);
        
        return result.Match<ActionResult<SaleDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            sale => sale is null 
                ? NotFound(new { message = "Not Found any sale." }) 
                   : Ok(sale));
    }
    
    [HttpPost]
    public async Task<ActionResult<SaleDto>> CreateSale([FromBody] CreateSaleRequest request, CancellationToken ct = default)
    {
        // Cycle 1.1 fix (bug #1): the previous version ignored the JWT companyId
        // and forwarded request.CompanyId directly. CreateSaleRequest no longer
        // exposes CompanyId, so the service cannot leak cross-tenant. The
        // companyId parameter is the JWT-derived tenant; it is the only source
        // of tenant scope at the application layer.
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        var result = await serviceManager.Sales.CreateSaleAsync(request, companyId, userId, ct);
        return result.Match<ActionResult<SaleDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            sale => CreatedAtAction(nameof(GetSale), new { id = sale.Id }, sale)
        );
    }
    
    [HttpPut("{id}")]
    public async Task<ActionResult<SaleDto>> UpdateSale(int id, [FromBody] UpdateSaleRequest request, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        var result = await serviceManager.Sales.UpdateSaleAsync(id, companyId, request, userId, ct);
        return result.Match<ActionResult<SaleDto>>(
            error => NotFound(new { message = error.Message }),
            sale => Ok(sale)
        );
    }
    
    [HttpPut("{id}/complete")]
    public async Task<ActionResult<SaleDto>> CompleteSale(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        var result = await serviceManager.Sales.CompleteSaleAsync(id, companyId, userId, ct);
        return result.Match<ActionResult<SaleDto>>(
            error => NotFound(new { message = error.Message }),
            sale => Ok(sale)
        );
    }
    
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteSale(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager.Sales.DeleteSaleAsync(id, companyId, ct);
        return result.Match<ActionResult>(
            error => NotFound(new { message = error.Message }),
            _ => NoContent()
        );
    }
}