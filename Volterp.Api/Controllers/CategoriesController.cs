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
        EnsureTenant();
        var result = await services.Categories.GetAllAsync(pagination.PageNumber, pagination.PageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryDto>> GetCategory(Guid id, CancellationToken ct)
    {
        EnsureTenant();
        var result = await services.Categories.GetByIdAsync(id, ct);
        return result.Match<ActionResult<CategoryDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            category => category is null ? NotFound(new ErrorResponse("Category not found")) 
                                                   : Ok(category)
        );
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory(
        [FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        if (!IsAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

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
        Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        if (!IsAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        var result = await services.Categories.UpdateAsync(id, request, companyId, userId, ct);
        return result.Match<ActionResult<CategoryDto>>(
            error => NotFound(new ErrorResponse(error.Message)),
            category => Ok(category)
        );
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
    {
        if (!IsAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("You do not have permission to perform this action."));

        var companyId = GetCurrentUserCompanyId();
        var result = await services.Categories.DeleteAsync(id, companyId, ct);
        return result.Match<IActionResult>(
            error => NotFound(new ErrorResponse(error.Message)),
            _ => NoContent()
        );
    }
}