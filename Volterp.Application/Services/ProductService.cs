using Microsoft.EntityFrameworkCore;
using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class ProductService(IUnitOfWork unitOfWork) : IProductService
{
    public async Task<PagedResult<ProductDto>> GetAllAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var products = await unitOfWork.Products.GetAllProductsByCompanyAsync(companyId, pageNumber, pageSize, ct);
        
         var dtos = products.Map(p => new ProductDto(p.Id,
                p.Name, p.Category,
                p.Description, p.Price,
                p.Stock, p.CategoryId,
                p.CompanyId,
                p.IsActive,
                p.ImageUrl, p.CreatedAt,
                p.UpdatedAt));

         return dtos;
    }

    public async Task<ProductDto?> GetByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        var product = await unitOfWork.Products.GetProductByIdAsync(id, ct);
        
        if (product is null || product.CompanyId != companyId)
            return null;
        
        var categoryName = product.CategoryId.HasValue
            ? (await unitOfWork.Categories.GetCategoryByIdAsync(product.CategoryId.Value, ct))?.Name
            : null;
        
        return product.Map(x=> new ProductDto(
            x.Id, x.Name, x.Category, x.Description,
            x.Price, x.Stock, x.CategoryId,
            x.CompanyId, x.IsActive, x.ImageUrl,
            x.CreatedAt, x.UpdatedAt
        ));
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, int companyId, CancellationToken ct = default)
    {
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await unitOfWork.Categories.ExistsCategoryAsync(request.CategoryId.Value, ct);
            if (!categoryExists)
                throw new ArgumentException("Category not found");
        }

        var product = new Product
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
        

        return new ProductDto(
            product.Id, product.Name, product.Category, product.Description,
            product.Price, product.Stock, product.CategoryId, product.CompanyId, product.IsActive, product.ImageUrl,
            product.CreatedAt, product.UpdatedAt
        );
    }

    public async Task<ProductDto> UpdateAsync(int id, UpdateProductRequest request, int companyId, CancellationToken ct = default)
    {
        var product = await unitOfWork.Products.GetProductByIdAsync(id, ct);
        
        if (product is null || product.CompanyId != companyId)
            throw new ArgumentException("Product not found");
        
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await unitOfWork.Categories.ExistsCategoryAsync(request.CategoryId.Value, ct);
            if (!categoryExists)
                throw new ArgumentException("Category not found");
        }

        product.Apply(r =>
        {
            r.Name = request.Name;
            r.Category = request.Category;
            r.Description = request.Description;
            r.Price = request.Price;
            r.Stock = request.Stock;
            r.CategoryId = request.CategoryId;
            r.ImageUrl = request.ImageUrl;
            r.IsActive = request.IsActive;
            r.UpdatedAt = DateTime.UtcNow;
        });
       

        await unitOfWork.Products.UpdateProductAsync(product, ct);
        await unitOfWork.CommitAsync(ct);
        

        return product.Map(x=> new ProductDto(
            product.Id, product.Name, product.Category, product.Description,
            product.Price, product.Stock, product.CategoryId,
            product.CompanyId, product.IsActive, product.ImageUrl,
            product.CreatedAt, product.UpdatedAt
        ));
    }

    public async Task DeleteAsync(int id, int companyId, CancellationToken ct = default)
    {
        var product = await unitOfWork.Products.GetProductByIdAsync(id, ct);
        
        if (product is null || product.CompanyId != companyId)
            throw new ArgumentException("Product not found");
        
        await unitOfWork.Products.DeleteProductAsync(id, ct);
        await unitOfWork.CommitAsync(ct);
    }
}