using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ProductDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;
using Volterp.ElectronicInvoicing;

namespace Volterp.Application.Services;

public class ProductService(IUnitOfWork unitOfWork) : IProductService
{
    private const string CodeAlreadyInUseMessage = "The code is already in use.";

    public async Task<PagedResult<ProductDto>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var products = await unitOfWork.Products.GetAllProductsAsync(pageNumber, pageSize, ct);
        return products.MapTo<Product, ProductDto>();
    }

    public async Task<Either<Error, ProductDto?>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async productId => await unitOfWork.Products.GetProductByIdAsync(productId, ct),
                ex => new Error(ex.Message))
            .Ensure(product => product is not null,
                new Error("Product not found."))
            .Map(product => product?.MapTo<Product, ProductDto>())
            .Run();
    }

    public async Task<Either<Error, ProductDto>> CreateAsync(CreateProductDto request, Guid companyId, Guid? userId, CancellationToken ct = default)
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
                product.TaxCode = req.TaxCode;

                // The user may bring its own code; otherwise the per-company sequence
                // provides one. A user-provided code must be free inside the company.
                product.Code = await ResolveCodeForCreateAsync(req.Code, companyId, ct);

                await unitOfWork.Products.AddProductAsync(product, ct);
                await unitOfWork.CommitAsync(ct);
                return product;
            }, ex => new Error(ex.Message))
            .Map(product => product.MapTo<Product, ProductDto>())
            .Run();
    }

    public async Task<Either<Error, ProductDto>> UpdateAsync(Guid id, UpdateProductDto request, Guid companyId, Guid? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async productId => await unitOfWork.Products.GetProductByIdAsync(productId, ct),
                ex => new Error(ex.Message))
            .Ensure(product => product is not null,
                new Error("Product not found."))
            .FlatMap(async product =>
            {
                if (request.CategoryId.HasValue)
                {
                    var categoryExists = await unitOfWork.Categories.ExistsCategoryAsync(request.CategoryId.Value, ct);
                    if (!categoryExists)
                        throw new Exception("Category not found.");
                }

                // An empty code keeps the current one; a new code must be free
                // inside the company (the row being updated is excluded).
                var resolvedCode = await ResolveCodeForUpdateAsync(request.Code, product.Code, companyId, product.Id, ct);

                product.Apply(r =>
                {
                    r.Name = request.Name;
                    r.Category = request.Category;
                    r.Description = request.Description;
                    r.Stock = request.Stock;
                    r.CategoryId = request.CategoryId;
                    r.ImageUrl = request.ImageUrl;
                    r.TaxCode = request.TaxCode;
                    r.Code = resolvedCode;
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

    public async Task<Either<Error, Unit>> DeleteAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async productId => await unitOfWork.Products.GetProductByIdAsync(productId, ct),
                ex => new Error(ex.Message))
            .Ensure(product => product is not null,
                new Error("Product not found."))
            .FlatMap(async product =>
            {
                await unitOfWork.Products.DeleteProductAsync(product.Id, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, ex => new Error(ex.Message))
            .Run();
    }

    /// <summary>
    /// Resolves the code of a new product: the user-provided one (trimmed) when it
    /// comes with a value, otherwise the next code of the company sequence.
    /// </summary>
    private async Task<string> ResolveCodeForCreateAsync(string? requestedCode, Guid companyId, CancellationToken ct)
    {
        var trimmed = requestedCode?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return await unitOfWork.Codes.NextAsync(companyId, CodeEntityType.Product, ct);

        if (await unitOfWork.Products.ExistsCodeAsync(companyId, trimmed, null, ct))
            throw new Exception(CodeAlreadyInUseMessage);

        return trimmed;
    }

    /// <summary>
    /// Resolves the code of an existing product: the user-provided one (trimmed) when it
    /// comes with a value, otherwise the code the product already has.
    /// </summary>
    private async Task<string> ResolveCodeForUpdateAsync(string? requestedCode, string currentCode, Guid companyId, Guid id, CancellationToken ct)
    {
        var trimmed = requestedCode?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return currentCode;

        if (await unitOfWork.Products.ExistsCodeAsync(companyId, trimmed, id, ct))
            throw new Exception(CodeAlreadyInUseMessage);

        return trimmed;
    }
}
