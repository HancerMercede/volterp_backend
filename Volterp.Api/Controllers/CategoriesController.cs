using Volterp.Application.DTOs.CategoryDtos;

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
        return result.Match<ActionResult<CategoryDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            category => category is null ? NotFound(new ErrorResponse("Category not found")) : Ok(category)
        );
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory(
        [FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        var result = await services.Categories.CreateAsync(request, companyId, userId, ct);
        return result.Match<ActionResult<CategoryDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            category => CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category)
        );
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(
        int id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        var result = await services.Categories.UpdateAsync(id, request, companyId, userId, ct);
        return result.Match<ActionResult<CategoryDto>>(
            error => NotFound(new ErrorResponse(error.Message)),
            category => Ok(category)
        );
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        var result = await services.Categories.DeleteAsync(id, companyId, ct);
        return result.Match<IActionResult>(
            error => NotFound(new ErrorResponse(error.Message)),
            _ => NoContent()
        );
    }
}