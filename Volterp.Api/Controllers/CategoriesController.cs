using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.CategoryDtos;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController(IServiceManager services) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CategoryDto>>> GetCategories(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct = default)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await services.Categories.GetAllAsync(companyId, pagination.PageNumber, pagination.PageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryDto>> GetCategory(int id, CancellationToken ct)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await services.Categories.GetByIdAsync(id, companyId, ct);
        
        if (result is null)
            return NotFound(new ErrorResponse("Category not found"));
        
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory(
        [FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        
        try
        {
            var result = await services.Categories.CreateAsync(request, companyId, ct);
            return CreatedAtAction(nameof(GetCategory), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(
        int id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        
        try
        {
            var result = await services.Categories.UpdateAsync(id, request, companyId, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        
        try
        {
            await services.Categories.DeleteAsync(id, companyId, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
    }
}