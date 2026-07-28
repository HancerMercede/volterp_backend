using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class PurchaseService(IUnitOfWork unitOfWork) : IPurchaseService
{
    private static PurchaseDto MapToPurchaseDto(Purchase p) => new PurchaseDto(
        p.Id, p.SupplierId, p.SupplierName, p.Status, p.Total, p.Notes,
        p.CreatedAt, p.UpdatedAt, p.CreatedBy, p.UpdatedBy,
        p.Items.Select(i => new PurchaseItemDto(
            i.Id, i.ProductId, i.ProductName, i.ProductCode,
            i.Quantity, i.UnitPrice, i.Subtotal
        )).ToList()
    );

    public async Task<PagedResult<PurchaseDto>> GetAllPurchasesAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var purchases = await unitOfWork.Purchases.GetAllPurchasesByCompanyAsync(companyId, pageNumber, pageSize, ct);
        return purchases.Map(MapToPurchaseDto);
    }

    public async Task<Either<Error, PurchaseDto?>> GetPurchaseByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async purchaseId => await unitOfWork.Purchases.GetPurchaseByIdAsync(purchaseId, companyId, ct),
                ex => new Error(ex.Message))
            .Map(purchase => purchase is null ? null : MapToPurchaseDto(purchase))
            .Run();
    }

    public async Task<Either<Error, PurchaseDto>> CreatePurchaseAsync(PurchaseDto request, int companyId, int? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, PurchaseDto>
            .FromRight(request)
            .Ensure(r => r.Items.Count > 0, new Error("Purchase must have at least one item."))
            .FlatMap(async req =>
            {
                var purchase = new Purchase
                {
                    CompanyId = companyId,
                    SupplierId = req.SupplierId,
                    SupplierName = req.SupplierName,
                    Status = req.Status,
                    Total = req.Total,
                    Notes = req.Notes,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };

                foreach (var item in req.Items)
                {
                    purchase.Items.Add(new PurchaseItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        ProductCode = item.ProductCode,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Subtotal = item.Subtotal
                    });
                }

                // Batch query: single SELECT for all products (avoids N+1)
                var productIds = purchase.Items
                    .Where(i => i.ProductId.HasValue)
                    .Select(i => i.ProductId!.Value)
                    .ToHashSet();

                var products = await unitOfWork.Products.GetProductsByIdsAsync(productIds, ct);
                var productMap = products.ToDictionary(p => p.Id);

                // Apply stock increments for each item (one item per product expected)
                foreach (var item in purchase.Items.Where(i => i.ProductId.HasValue))
                {
                    if (productMap.TryGetValue(item.ProductId!.Value, out var product))
                        product.IncrementStock(item.Quantity);
                }

                await unitOfWork.Purchases.AddPurchaseAsync(purchase, ct);
                await unitOfWork.CommitAsync(ct);
                return purchase;
            }, ex => new Error(ex.Message))
            .Map(purchase => MapToPurchaseDto(purchase))
            .Run();
    }

    public async Task<Either<Error, PurchaseDto>> UpdatePurchaseAsync(int id, int companyId, PurchaseDto request, int? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async purchaseId => await unitOfWork.Purchases.GetPurchaseByIdAsync(purchaseId, companyId, ct),
                ex => new Error(ex.Message))
            .Ensure(purchase => purchase is not null, new Error("Purchase not found."))
            .FlatMap(async purchase =>
            {
                // Collect all product IDs (old items + new items)
                var oldProductIds = purchase.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value);
                var newProductIds = request.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value);
                var allProductIds = oldProductIds.Concat(newProductIds).ToHashSet();

                // Batch query: single SELECT for all products involved
                var products = await unitOfWork.Products.GetProductsByIdsAsync(allProductIds, ct);
                var productMap = products.ToDictionary(p => p.Id);

                // First: restore stock from old items being removed (using DeductStock with allowNegative for reversal)
                foreach (var item in purchase.Items.Where(i => i.ProductId.HasValue))
                {
                    if (productMap.TryGetValue(item.ProductId!.Value, out var product))
                        product.DeductStock(item.Quantity, allowNegative: true);
                }

                // Then: add stock from new items
                foreach (var item in request.Items.Where(i => i.ProductId.HasValue))
                {
                    if (productMap.TryGetValue(item.ProductId!.Value, out var product))
                        product.IncrementStock(item.Quantity);
                }

                var newItems = request.Items.Select(p => new PurchaseItem
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    ProductCode = p.ProductCode,
                    Quantity = p.Quantity,
                    UnitPrice = p.UnitPrice,
                    Subtotal = p.Subtotal
                }).ToList();

                purchase.Apply(p =>
                {
                    p.SupplierId = request.SupplierId;
                    p.SupplierName = request.SupplierName;
                    p.Status = request.Status;
                    p.Total = request.Total;
                    p.Notes = request.Notes;
                    p.UpdatedAt = DateTime.UtcNow;
                    p.UpdatedBy = userId;
                    p.Items = newItems;
                });

                await unitOfWork.Purchases.UpdatePurchaseAsync(purchase, ct);
                await unitOfWork.CommitAsync(ct);
                return purchase;
            }, ex => new Error(ex.Message))
            .Map(purchase => MapToPurchaseDto(purchase))
            .Run();
    }

    public async Task<Either<Error, Unit>> DeletePurchaseAsync(int id, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async purchaseId => await unitOfWork.Purchases.GetPurchaseByIdAsync(purchaseId, companyId, ct),
                ex => new Error(ex.Message))
            .Ensure(purchase => purchase is not null, new Error("Purchase not found."))
            .FlatMap(async purchase =>
            {
                // Only decrement stock if there are items with product IDs
                var productIds = purchase.Items
                    .Where(i => i.ProductId.HasValue)
                    .Select(i => i.ProductId!.Value)
                    .ToHashSet();

                if (productIds.Count > 0)
                {
                    var products = await unitOfWork.Products.GetProductsByIdsAsync(productIds, ct);
                    var productMap = products.ToDictionary(p => p.Id);

                    // Decrement stock for each item using allowNegative for delete-recovery path
                    foreach (var item in purchase.Items.Where(i => i.ProductId.HasValue))
                    {
                        if (productMap.TryGetValue(item.ProductId!.Value, out var product))
                            product.DeductStock(item.Quantity, allowNegative: true);
                    }
                }

                await unitOfWork.Purchases.DeletePurchaseAsync(id, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, ex => new Error(ex.Message))
            .Run();
    }
}