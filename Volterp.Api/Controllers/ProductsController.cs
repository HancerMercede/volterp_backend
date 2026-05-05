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
public class ProductsController(IUnitOfWork unitOfWork) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> GetProducts(CancellationToken ct)
    {
        var companyId = GetCurrentUserCompanyId();
        var products = await unitOfWork.Products.GetAllProductsByCompanyAsync(companyId, ct);

        var productsDtos = new List<ProductDto>();
        foreach (var p in products)
        {
            var categoryName = p.CategoryId.HasValue
                ? (await unitOfWork.Categories.GetCategoryByIdAsync(p.CategoryId.Value, ct))?.Name
                : null;
        
            productsDtos.Add(new ProductDto(
                p.Id, p.Name, p.Category, p.Description, p.Price, p.Stock,
                p.CategoryId, categoryName, p.CompanyId, p.IsActive,
                p.ImageUrl, p.CreatedAt, p.UpdatedAt
            ));
        }
        return Ok(productsDtos);
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

        var productDto = product.Map(product => new ProductDto(
            product.Id, product.Name, product.Category, product.Description,
            product.Price, product.Stock, product.CategoryId, categoryName,
            product.CompanyId, product.IsActive, product.ImageUrl, product.CreatedAt, product.UpdatedAt
            ));
        return Ok(productDto);
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

        var product = request.Map(p => new Product
        {
            Name = p.Name,
            Category = p.Category,
            Description = p.Description,
            Price = p.Price,
            Stock = p.Stock,
            CategoryId = p.CategoryId,
            ImageUrl = p.ImageUrl,
            CompanyId = companyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await unitOfWork.Products.AddProductAsync(product, ct);
        await unitOfWork.CommitAsync(ct);

        var categoryName = product.CategoryId.HasValue
            ? (await unitOfWork.Categories.GetCategoryByIdAsync(product.CategoryId.Value, ct))?.Name
            : null;

        var dto = product.Map(p => new ProductDto
        (
            p.Id, p.Name, p.Category, p.Description,
            p.Price, p.Stock, p.CategoryId, categoryName,
            p.CompanyId, p.IsActive, p.ImageUrl, p.CreatedAt, p.UpdatedAt
        ));

        return CreatedAtAction(nameof(GetProduct), new { id = dto.Id }, dto);
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

        product.Apply(p =>
        {
            p.Name = request.Name;
            p.Category = request.Category;
            p.Description = request.Description;
            p.Price = request.Price;
            p.Stock = request.Stock;
            p.CategoryId = request.CategoryId;
            p.ImageUrl = request.ImageUrl;
            p.IsActive = request.IsActive;
            p.UpdatedAt = DateTime.UtcNow;
        });
        

        await unitOfWork.Products.UpdateProductAsync(product, ct);
        await unitOfWork.CommitAsync(ct);

        var categoryName = product.CategoryId.HasValue
            ? (await unitOfWork.Categories.GetCategoryByIdAsync(product.CategoryId.Value, ct))?.Name
            : null;

        var productDto = product.Map(p => new ProductDto(
            p.Id, p.Name, p.Category, p.Description,
            p.Price, p.Stock, p.CategoryId, categoryName,
            p.CompanyId, p.IsActive, p.ImageUrl, p.CreatedAt, p.UpdatedAt
        ));
        return Ok(productDto);
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