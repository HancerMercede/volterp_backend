using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesController(IServiceManager serviceManager) : BaseController
{
    /// <summary>
    /// Obtener todas las ventas de la empresa (paginado)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<SaleDto>>> GetAllSales(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager.Sales.GetAllSalesAsync(companyId, pagination.PageNumber, pagination.PageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Obtener ventas por estado (Pending/Completed)
    /// </summary>
    [HttpGet("status/{status}")]
    public async Task<ActionResult<PagedResult<SaleDto>>> GetSalesByStatus(
        Domain.Entities.SaleStatus status,
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager.Sales.GetSalesByStatusAsync(companyId, status, pagination.PageNumber, pagination.PageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Obtener ventas pendientes (borrador)
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<PagedResult<SaleDto>>> GetPendingSales(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager.Sales.GetSalesByStatusAsync(companyId, SaleStatus.Pending, pagination.PageNumber, pagination.PageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Obtener una venta por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<SaleDto>> GetSale(int id, CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var sale = await serviceManager.Sales.GetSaleByIdAsync(id, companyId, ct);
        
        if (sale is null)
            return NotFound(new { message = "Venta no encontrada" });
            
        return Ok(sale);
    }

    /// <summary>
    /// Crear nueva venta (se guarda como pendiente/borrador)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SaleDto>> CreateSale([FromBody] CreateSaleRequest request, CancellationToken ct = default)
    {
        var sale = await serviceManager.Sales.CreateSaleAsync(request, ct);
        return CreatedAtAction(nameof(GetSale), new { id = sale.Id }, sale);
    }

    /// <summary>
    /// Actualizar venta (agregar/quitar productos de un borrador)
    /// </summary>
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

    /// <summary>
    /// Completar una venta (marcar como finalizada/cerrada)
    /// </summary>
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

    /// <summary>
    /// Eliminar una venta
    /// </summary>
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