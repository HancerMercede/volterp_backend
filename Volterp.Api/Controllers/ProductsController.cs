using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volterp.Application.DTOs;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController(IUnitOfWork unitOfWork) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> GetProducts(CancellationToken ct)
    {
        var companyId = GetCurrentUserCompanyId();
        var products = await unitOfWork.Products.GetAllProductsByCompanyAsync(companyId, ct);

        var dtos = new List<ProductDto>();
        foreach (var p in products)
        {
            var categoryName = p.CategoryId.HasValue
                ? (await unitOfWork.Categories.GetCategoryByIdAsync(p.CategoryId.Value, ct))?.Name
                : null;

            dtos.Add(new ProductDto(
                p.Id, p.Name, p.Category, p.Description, p.Price, p.Stock,
                p.CategoryId, categoryName, p.CompanyId, p.IsActive,
                p.ImageUrl, p.CreatedAt, p.UpdatedAt
            ));
        }

        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetProduct(int id, CancellationToken ct)
    {
        var companyId = GetCurrentUserCompanyId();
        var product = await unitOfWork.Products.GetProductByIdAsync(id, ct);

        if (product is null || product.CompanyId != companyId)
            return NotFound(new ErrorResponse("Product not found"));

        var categoryName = product.CategoryId.HasValue
            ? (await unitOfWork.Categories.GetCategoryByIdAsync(product.CategoryId.Value, ct))?.Name
            : null;

        return Ok(new ProductDto(
            product.Id, product.Name, product.Category, product.Description,
            product.Price, product.Stock, product.CategoryId, categoryName,
            product.CompanyId, product.IsActive, product.ImageUrl, product.CreatedAt, product.UpdatedAt
        ));
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> CreateProduct(
        [FromBody] CreateProductRequest request, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();

        if (request.CategoryId.HasValue)
        {
            var categoryExists = await unitOfWork.Categories.ExistsCategoryAsync(request.CategoryId.Value, ct);
            if (!categoryExists)
                return BadRequest(new ErrorResponse("Category not found"));
        }

        var product = new Domain.Entities.Product
        {
            Name = request.Name,
            Category = request.Category,
            Description = request.Description,
            Price = request.Price,
            Stock = request.Stock,
            CategoryId = request.CategoryId,
            ImageUrl = request.ImageUrl,
            CompanyId = companyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await unitOfWork.Products.AddProductAsync(product, ct);
        await unitOfWork.CommitAsync(ct);

        var categoryName = product.CategoryId.HasValue
            ? (await unitOfWork.Categories.GetCategoryByIdAsync(product.CategoryId.Value, ct))?.Name
            : null;

        var dto = new ProductDto(
            product.Id, product.Name, product.Category, product.Description,
            product.Price, product.Stock, product.CategoryId, categoryName,
            product.CompanyId, product.IsActive, product.ImageUrl, product.CreatedAt, product.UpdatedAt
        );

        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, dto);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProductDto>> UpdateProduct(
        int id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        var product = await unitOfWork.Products.GetProductByIdAsync(id, ct);

        if (product is null || product.CompanyId != companyId)
            return NotFound(new ErrorResponse("Product not found"));

        if (request.CategoryId.HasValue)
        {
            var categoryExists = await unitOfWork.Categories.ExistsCategoryAsync(request.CategoryId.Value, ct);
            if (!categoryExists)
                return BadRequest(new ErrorResponse("Category not found"));
        }

        product.Name = request.Name;
        product.Category = request.Category;
        product.Description = request.Description;
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.CategoryId = request.CategoryId;
        product.ImageUrl = request.ImageUrl;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await unitOfWork.Products.UpdateProductAsync(product, ct);
        await unitOfWork.CommitAsync(ct);

        var categoryName = product.CategoryId.HasValue
            ? (await unitOfWork.Categories.GetCategoryByIdAsync(product.CategoryId.Value, ct))?.Name
            : null;

        return Ok(new ProductDto(
            product.Id, product.Name, product.Category, product.Description,
            product.Price, product.Stock, product.CategoryId, categoryName,
            product.CompanyId, product.IsActive, product.ImageUrl, product.CreatedAt, product.UpdatedAt
        ));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var companyId = GetCurrentUserCompanyId();
        var product = await unitOfWork.Products.GetProductByIdAsync(id, ct);

        if (product is null || product.CompanyId != companyId)
            return NotFound(new ErrorResponse("Product not found"));

        await unitOfWork.Products.DeleteProductAsync(id, ct);
        await unitOfWork.CommitAsync(ct);

        return NoContent();
    }
}