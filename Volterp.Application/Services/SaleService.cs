using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.Services;

public class SaleService(IUnitOfWork unitOfWork) : ISaleService
{

    private static List<SaleItem> MapToSaleItems(List<CreateSaleItemRequest> items) => items
        .Select(i => new SaleItem
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            ProductCategory = i.ProductCategory,
            ProductCode = i.ProductCode,
            ProductImageUrl = i.ProductImageUrl,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Subtotal = i.Subtotal
        }).ToList();

    public async Task<PagedResult<SaleDto>> GetAllSalesAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var sales = await unitOfWork.Sales.GetAllSalesByCompanyAsync(companyId, pageNumber, pageSize, ct);
        return sales.MapTo<Sale, SaleDto>();
    }

    public async Task<PagedResult<SaleDto>> GetSalesByStatusAsync(int companyId, SaleStatus status, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var sales = await unitOfWork.Sales.GetSalesByStatusAsync(companyId, status, pageNumber, pageSize, ct);
        return sales.MapTo<Sale, SaleDto>();
    }

    public async Task<Either<Error, SaleDto?>> GetSaleByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async saleId => await unitOfWork.Sales.GetSaleByIdAsync(saleId, companyId, ct),
                ex => new Error(ex.Message))
            .Map(sale => sale?.MapTo<Sale, SaleDto>())
            .Run();
    }

    public async Task<Either<Error, SaleDto>> CreateSaleAsync(CreateSaleRequest request, int companyId, int? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, CreateSaleRequest>
            .FromRight(request)
            .Ensure(r => r.Items.Count > 0, new Error("Sale must have at least one item."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async req =>
            {
                var sale = req.Project();
                // Cycle 1.1: companyId comes from the controller (JWT-derived), not
                // from the request body. The request DTO no longer carries CompanyId.
                sale.CompanyId = companyId;
                if (userId.HasValue) sale.SetCreatedBy(userId.Value);

                // Batch query: single SELECT for all products (avoids N+1)
                var productIds = sale.Items.Select(i => i.ProductId).ToHashSet();
                var products = await unitOfWork.Products.GetProductsByIdsAsync(productIds, ct);
                var productMap = products.ToDictionary(p => p.Id);

                // Validate and deduct stock for each item
                // NOTE: Stock is deducted at creation time to reserve inventory.
                // The double-deduction behavior (at creation AND completion) is preserved.
                foreach (var item in sale.Items)
                {
                    if (!productMap.TryGetValue(item.ProductId, out var product))
                        continue;

                    product.DeductStock(item.Quantity);
                }

                await unitOfWork.Sales.AddSaleAsync(sale, ct);
                await unitOfWork.CommitAsync(ct);
                return sale;
            }, ex => new Error(ex.Message))
            .Map(sale =>sale.MapTo<Sale, SaleDto>())
            .Run();
    }

    public async Task<Either<Error, SaleDto>> UpdateSaleAsync(int id, int companyId, UpdateSaleRequest request, int? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async saleId => await unitOfWork.Sales.GetSaleByIdAsync(saleId, companyId, ct),
                ex => new Error(ex.Message))
            .Ensure(sale => sale is not null, new Error("Sale not found."))
            .FlatMap(async sale =>
            {
                // Collect all product IDs (old items + new items)
                var allProductIds = sale.Items.Select(i => i.ProductId)
                    .Concat(request.Items.Select(i => i.ProductId))
                    .ToHashSet();

                // Batch query: single SELECT for all products involved
                var products = await unitOfWork.Products.GetProductsByIdsAsync(allProductIds, ct);
                var productMap = products.ToDictionary(p => p.Id);

                // Build product name lookup for error messages
                var newItemNames = request.Items.ToDictionary(i => i.ProductId, i => i.ProductName);

                // Apply old returns: restore stock from items being removed
                foreach (var item in sale.Items)
                {
                    if (productMap.TryGetValue(item.ProductId, out var product))
                        product.IncrementStock(item.Quantity);
                }

                // Validate and apply new deductions (group by productId to handle duplicates)
                var newDeductions = request.Items
                    .GroupBy(i => i.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

                foreach (var (productId, quantity) in newDeductions)
                {
                    if (!productMap.TryGetValue(productId, out var product))
                        continue;

                    product.DeductStock(quantity);
                }
                
                var newItems = MapToSaleItems(request.Items);

                sale.Apply(s =>
                {
                    s.ClienteId = request.ClienteId;
                    s.ClienteName = request.ClienteName;
                    s.Status = request.Status;
                    s.Total = request.Total;
                    s.Notes = request.Notes;
                    s.UpdatedAt = DateTime.UtcNow;
                    if (userId.HasValue) s.SetUpdatedBy(userId.Value);
                    s.Items = newItems;
                });

                await unitOfWork.Sales.UpdateSaleAsync(sale, ct);
                await unitOfWork.CommitAsync(ct);
                return sale;
            }, ex => new Error(ex.Message))
            .Map(sale => sale.MapTo<Sale, SaleDto>())
            .Run();
    }

    public async Task<Either<Error, SaleDto>> CompleteSaleAsync(int id, int companyId, int? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async saleId => await unitOfWork.Sales.GetSaleByIdAsync(saleId, companyId, ct),
                ex => new Error(ex.Message))
            .Ensure(sale => sale is not null, new Error("Sale not found."))
            .FlatMap(async sale =>
            {
                sale.Complete();
                // Validate and deduct stock for each item
                var productIds = sale.Items.Select(i => i.ProductId).ToHashSet();
                if (productIds.Count > 0)
                {
                    var products = await unitOfWork.Products.GetProductsByIdsAsync(productIds, ct);
                    var productMap = products.ToDictionary(p => p.Id);

                    foreach (var item in sale.Items)
                    {
                        if (!productMap.TryGetValue(item.ProductId, out var product))
                            continue;

                        product.DeductStock(item.Quantity);
                    }
                }

                sale.Apply(s =>
                {
                    s.UpdatedAt = DateTime.UtcNow;
                    if (userId.HasValue) s.SetUpdatedBy(userId.Value);
                });

                await unitOfWork.Sales.UpdateSaleAsync(sale, ct);
                await unitOfWork.CommitAsync(ct);
                return sale;
            }, ex => new Error(ex.Message))
            .Map(sale => sale.MapTo<Sale, SaleDto>())
            .Run();
    }

    public async Task<Either<Error, Unit>> DeleteSaleAsync(int id, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async saleId => await unitOfWork.Sales.GetSaleByIdAsync(saleId, companyId, ct),
                ex => new Error(ex.Message))
            .Ensure(sale => sale is not null, new Error("Sale not found."))
            .FlatMap(async sale =>
            {
                // Only restore stock if there are items with product IDs
                var productIds = sale.Items.Select(i => i.ProductId).ToHashSet();
                if (productIds.Count > 0)
                {
                    var products = await unitOfWork.Products.GetProductsByIdsAsync(productIds, ct);
                    var productMap = products.ToDictionary(p => p.Id);

                    foreach (var item in sale.Items)
                    {
                        if (productMap.TryGetValue(item.ProductId, out var product))
                            product.IncrementStock(item.Quantity);
                    }
                }

                await unitOfWork.Sales.DeleteSaleAsync(id, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, ex => new Error(ex.Message))
            .Run();
    }
}