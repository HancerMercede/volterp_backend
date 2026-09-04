using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ProductDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;

namespace Volterp.Application.Services;

public class ProductService(IUnitOfWork unitOfWork) : IProductService
{
    public async Task<PagedResult<ProductDto>> GetAllAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var products = await unitOfWork.Products.GetAllProductsByCompanyAsync(companyId, pageNumber, pageSize, ct);
        return products.MapTo<Product, ProductDto>();
    }

    public async Task<Either<Error, ProductDto?>> GetByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async productId => await unitOfWork.Products.GetProductByIdAsync(productId, ct),
                ex => new Error(ex.Message))
            .Ensure(product => product is not null && product.CompanyId == companyId,
                new Error("Product not found."))
            .Map(product => product?.MapTo<Product, ProductDto>())
            .Run();
    }

    public async Task<Either<Error, ProductDto>> CreateAsync(CreateProductDto request, int companyId, int? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, CreateProductDto>
            .FromRight(request)
            .Ensure(r => !string.IsNullOrWhiteSpace(r.Name), new Error("The name is required."))
            .FlatMap(async req =>
            {
                if (req.CategoryId.HasValue)
                {
                    var categoryExists = await unitOfWork.Categories.ExistsCategoryAsync(req.CategoryId.Value, ct);
                    if (!categoryExists)
                        throw new Exception("Category not found.");
                }

                var product = req.Project();
                product.CompanyId = companyId;
                if (userId.HasValue) product.SetCreatedBy(userId.Value);
                product.SetPrice(new Money(req.Price, Currency.ARS));

                await unitOfWork.Products.AddProductAsync(product, ct);
                await unitOfWork.CommitAsync(ct);
                return product;
            }, ex => new Error(ex.Message))
            .Map(product => product.MapTo<Product, ProductDto>())
            .Run();
    }

    public async Task<Either<Error, ProductDto>> UpdateAsync(int id, UpdateProductDto request, int companyId, int? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async productId => await unitOfWork.Products.GetProductByIdAsync(productId, ct),
                ex => new Error(ex.Message))
            .Ensure(product => product is not null && product.CompanyId == companyId,
                new Error("Product not found."))
            .FlatMap(async product =>
            {
                if (request.CategoryId.HasValue)
                {
                    var categoryExists = await unitOfWork.Categories.ExistsCategoryAsync(request.CategoryId.Value, ct);
                    if (!categoryExists)
                        throw new Exception("Category not found.");
                }

                product.Apply(r =>
                {
                    r.Name = request.Name;
                    r.Category = request.Category;
                    r.Description = request.Description;
                    r.Stock = request.Stock;
                    r.CategoryId = request.CategoryId;
                    r.ImageUrl = request.ImageUrl;
                    r.UpdatedAt = DateTime.UtcNow;
                    if (userId.HasValue) r.SetUpdatedBy(userId.Value);
                });
                product.SetPrice(new Money(request.Price, Currency.ARS));
                if (request.IsActive) product.Activate(); else product.Deactivate();

                await unitOfWork.Products.UpdateProductAsync(product, ct);
                await unitOfWork.CommitAsync(ct);
                return product;
            }, ex => new Error(ex.Message))
            .Map(product => product.MapTo<Product, ProductDto>())
            .Run();
    }

    public async Task<Either<Error, Unit>> DeleteAsync(int id, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async productId => await unitOfWork.Products.GetProductByIdAsync(productId, ct),
                ex => new Error(ex.Message))
            .Ensure(product => product is not null && product.CompanyId == companyId,
                new Error("Product not found."))
            .FlatMap(async product =>
            {
                await unitOfWork.Products.DeleteProductAsync(product.Id, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, ex => new Error(ex.Message))
            .Run();
    }
}