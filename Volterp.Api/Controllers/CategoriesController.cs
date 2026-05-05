using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Helpers;
using Volterp.Application.DTOs;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

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

        var categoriesDtos = categories.Select(c => 
            new CategoryDto(
            c.Id, c.Name, c.Description, c.CompanyId, c.IsActive, c.CreatedAt
        )).ToList();
        
        
        return Ok(categoriesDtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryDto>> GetCategory(int id, CancellationToken ct)
    {
        var companyId = GetCurrentUserCompanyId();
        var category = await unitOfWork.Categories.GetCategoryByIdAsync(id, ct);

        if (category is null || category.CompanyId != companyId)
            return NotFound(new ErrorResponse("Category not found"));

        var categoryDto = category.Map(c => new CategoryDto(
            c.Id, c.Name, c.Description,
            c.CompanyId, c.IsActive, c.CreatedAt
        ));
        return Ok(categoryDto);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory(
        [FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();

        var category = request.Map(c=> new Category
        {
            Name = c.Name,
            Description = c.Description,
            CompanyId = companyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await unitOfWork.Categories.AddCategoryAsync(category, ct);
        await unitOfWork.CommitAsync(ct);

        var categoryDto = category.Map(c=> new CategoryDto(
            c.Id, c.Name, c.Description,
            c.CompanyId, c.IsActive, c.CreatedAt
        ));

        return CreatedAtAction(nameof(GetCategory), new { id = categoryDto.Id }, categoryDto);
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

        category.Apply(c =>
        {
            c.Name = request.Name;
            c.Description = request.Description;
            c.IsActive = request.IsActive;
        });
        
        

        await unitOfWork.Categories.UpdateCategoryAsync(category, ct);
        await unitOfWork.CommitAsync(ct);

        var categoryDto = category.Map(c => new CategoryDto(
            c.Id, c.Name, c.Description,
            c.CompanyId, c.IsActive, c.CreatedAt
        ));
        return Ok(categoryDto);
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

        await unitOfWork.Categories.DeleteCategoryAsync(category.Id, ct);
        await unitOfWork.CommitAsync(ct);

        return NoContent();
    }
}