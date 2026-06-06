using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ProductDtos;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController(IServiceManager services) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetProducts(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await services.Products.GetAllAsync(companyId, pagination.PageNumber, pagination.PageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetProduct(int id, CancellationToken ct)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await services.Products.GetByIdAsync(id, companyId, ct);
        
        if (result is null)
            return NotFound(new ErrorResponse("Product not found"));
        
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> CreateProduct(
        [FromBody] CreateProductDto request, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        
        try
        {
            var result = await services.Products.CreateAsync(request, companyId, ct);
            return CreatedAtAction(nameof(GetProduct), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProductDto>> UpdateProduct(
        int id, [FromBody] UpdateProductDto request, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        
        try
        {
            var result = await services.Products.UpdateAsync(id, request, companyId, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        
        try
        {
            await services.Products.DeleteAsync(id, companyId, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
    }
}