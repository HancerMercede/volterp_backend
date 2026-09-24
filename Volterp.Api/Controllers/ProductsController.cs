using Volterp.Application.DTOs.ProductDtos;

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
    public async Task<ActionResult<ProductDto>> GetProduct(Guid id, CancellationToken ct)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await services.Products.GetByIdAsync(id, companyId, ct);
        return result.Match<ActionResult<ProductDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            product => product is null ? NotFound(new ErrorResponse("Product not found")) : Ok(product)
        );
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> CreateProduct(
        [FromBody] CreateProductDto request, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        var result = await services.Products.CreateAsync(request, companyId, userId, ct);
        return result.Match<ActionResult<ProductDto>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            product => CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product)
        );
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProductDto>> UpdateProduct(
        Guid id, [FromBody] UpdateProductDto request, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        var userId = GetCurrentUserId();
        var result = await services.Products.UpdateAsync(id, request, companyId, userId, ct);
        return result.Match<ActionResult<ProductDto>>(
            error => NotFound(new ErrorResponse(error.Message)),
            product => Ok(product)
        );
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        var result = await services.Products.DeleteAsync(id, companyId, ct);
        return result.Match<IActionResult>(
            error => NotFound(new ErrorResponse(error.Message)),
            _ => NoContent()
        );
    }
}