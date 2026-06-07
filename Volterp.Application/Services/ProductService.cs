using Microsoft.EntityFrameworkCore;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ProductDtos;
using Volterp.Application.Exceptions.Category;
using Volterp.Application.Exceptions.Product;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class ProductService(IUnitOfWork unitOfWork) : IProductService
{
    public async Task<PagedResult<ProductDto>> GetAllAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var products = await unitOfWork.Products.GetAllProductsByCompanyAsync(companyId, pageNumber, pageSize, ct);
        
        return products.MapTo<Product,  ProductDto>();
    }

    public async Task<ProductDto?> GetByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        var product = await unitOfWork.Products.GetProductByIdAsync(id, ct);
        
        if (product is null || product.CompanyId != companyId)
            throw new ProductNotFoundException("Product not found");
        
        return product.MapTo<Product,  ProductDto>();
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto request, int companyId, CancellationToken ct = default)
    {
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await unitOfWork.Categories.ExistsCategoryAsync(request.CategoryId.Value, ct);
            if (!categoryExists)
                throw new CategoryNotFoundException("Category not found");
        }

        var product = request.Project();
        product.CompanyId = companyId;
        
        await unitOfWork.Products.AddProductAsync(product, ct);
        await unitOfWork.CommitAsync(ct);
        
        return product.MapTo<Product, ProductDto>();
    }

    public async Task<ProductDto> UpdateAsync(int id, UpdateProductDto request, int companyId, CancellationToken ct = default)
    {
        var product = await unitOfWork.Products.GetProductByIdAsync(id, ct);
        
        if (product is null || product.CompanyId != companyId)
            throw new ProductNotFoundException("Product not found");
        
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await unitOfWork.Categories.ExistsCategoryAsync(request.CategoryId.Value, ct);
            if (!categoryExists)
                throw new CategoryNotFoundException("Category not found");
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
        
        return product.MapTo<Product, ProductDto>();
    }

    public async Task DeleteAsync(int id, int companyId, CancellationToken ct = default)
    {
        var product = await unitOfWork.Products.GetProductByIdAsync(id, ct);
        
        if (product is null || product.CompanyId != companyId)
            throw new ProductNotFoundException("Product not found");
        
        await unitOfWork.Products.DeleteProductAsync(id, ct);
        await unitOfWork.CommitAsync(ct);
    }
}