using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volterp.Application.DTOs;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController(IUnitOfWork unitOfWork) : BaseController
{

    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories(CancellationToken ct)
    {
        var companyId = GetCurrentUserCompanyId();
        var categories = await unitOfWork.Categories.GetAllCategoriesByCompanyAsync(companyId, ct);

        var dtos = categories.Select(c => new CategoryDto(
            c.Id, c.Name, c.Description, c.CompanyId, c.IsActive, c.CreatedAt
        )).ToList();

        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryDto>> GetCategory(int id, CancellationToken ct)
    {
        var companyId = GetCurrentUserCompanyId();
        var category = await unitOfWork.Categories.GetCategoryByIdAsync(id, ct);

        if (category is null || category.CompanyId != companyId)
            return NotFound(new ErrorResponse("Category not found"));

        return Ok(new CategoryDto(
            category.Id, category.Name, category.Description,
            category.CompanyId, category.IsActive, category.CreatedAt
        ));
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory(
        [FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();

        var category = new Domain.Entities.Category
        {
            Name = request.Name,
            Description = request.Description,
            CompanyId = companyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await unitOfWork.Categories.AddCategoryAsync(category, ct);
        await unitOfWork.CommitAsync(ct);

        var dto = new CategoryDto(
            category.Id, category.Name, category.Description,
            category.CompanyId, category.IsActive, category.CreatedAt
        );

        return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, dto);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(
        int id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        var category = await unitOfWork.Categories.GetCategoryByIdAsync(id, ct);

        if (category is null || category.CompanyId != companyId)
            return NotFound(new ErrorResponse("Category not found"));

        category.Name = request.Name;
        category.Description = request.Description;
        category.IsActive = request.IsActive;

        await unitOfWork.Categories.UpdateCategoryAsync(category, ct);
        await unitOfWork.CommitAsync(ct);

        return Ok(new CategoryDto(
            category.Id, category.Name, category.Description,
            category.CompanyId, category.IsActive, category.CreatedAt
        ));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        var category = await unitOfWork.Categories.GetCategoryByIdAsync(id, ct);

        if (category is null || category.CompanyId != companyId)
            return NotFound(new ErrorResponse("Category not found"));

        await unitOfWork.Categories.DeleteCategoryAsync(id, ct);
        await unitOfWork.CommitAsync(ct);

        return NoContent();
    }
}