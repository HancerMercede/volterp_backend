using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class PurchaseService(IUnitOfWork unitOfWork) : IPurchaseService
{
    public async Task<PagedResult<PurchaseDto>> GetAllPurchasesAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var purchases = await unitOfWork.Purchases.GetAllPurchasesByCompanyAsync(companyId, pageNumber, pageSize, ct);

        return purchases.Map(p => new PurchaseDto(
            p.Id, p.SupplierId, p.SupplierName, p.Status, p.Total, p.Notes,
            p.CreatedAt, p.UpdatedAt, p.CreatedBy, p.UpdatedBy,
            p.Items.Select(i => new PurchaseItemDto(
                i.Id, i.ProductId, i.ProductName, i.ProductCode,
                i.Quantity, i.UnitPrice, i.Subtotal
            )).ToList()
        ));
    }

    public async Task<PurchaseDto?> GetPurchaseByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        var purchase = await unitOfWork.Purchases.GetPurchaseByIdAsync(id, companyId, ct);

        if (purchase is null) return null;

        return purchase.Map(x=> new PurchaseDto(
            x.Id, x.SupplierId, x.SupplierName, x.Status, x.Total, x.Notes,
            x.CreatedAt, x.UpdatedAt, x.CreatedBy, x.UpdatedBy,
            x.Items.Select(i => new PurchaseItemDto(
                i.Id, i.ProductId, i.ProductName, i.ProductCode,
                i.Quantity, i.UnitPrice, i.Subtotal
            )).ToList()
        ));
    }

    public async Task<PurchaseDto> CreatePurchaseAsync(PurchaseDto request, int companyId, int? userId, CancellationToken ct = default)
    {
        var purchase = new Purchase
        {
            CompanyId = companyId,
            SupplierId = request.SupplierId,
            SupplierName = request.SupplierName,
            Status = request.Status,
            Total = request.Total,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        foreach (var item in request.Items)
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

       
        foreach (var item in purchase.Items)
        {
            if (item.ProductId.HasValue && productMap.TryGetValue(item.ProductId.Value, out var product))
                product.Stock += item.Quantity;
        }

        // EF change tracker persists changes automatically on CommitAsync
        await unitOfWork.Purchases.AddPurchaseAsync(purchase, ct);
        await unitOfWork.CommitAsync(ct);

        return purchase.Map(x=> new PurchaseDto(
            x.Id, x.SupplierId, x.SupplierName, x.Status, x.Total, x.Notes,
            x.CreatedAt, x.UpdatedAt, x.CreatedBy, x.UpdatedBy,
            x.Items.Select(i => new PurchaseItemDto(
                i.Id, i.ProductId, i.ProductName, i.ProductCode,
                i.Quantity, i.UnitPrice, i.Subtotal
            )).ToList()
        ));
    }

    public async Task<PurchaseDto> UpdatePurchaseAsync(int id, int companyId, PurchaseDto request, int? userId, CancellationToken ct = default)
    {
        var purchase = await unitOfWork.Purchases.GetPurchaseByIdAsync(id, companyId, ct);

        if (purchase is null)
            throw new ArgumentException("Purchase not found");

        // Collect all product IDs (old items + new items)
        var oldProductIds = purchase.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value);
        var newProductIds = request.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value);
        var allProductIds = oldProductIds.Concat(newProductIds).ToHashSet();

        // Batch query: single SELECT for all products involved
        var products = await unitOfWork.Products.GetProductsByIdsAsync(allProductIds, ct);
        var productMap = products.ToDictionary(p => p.Id);

        // Subtract old item quantities from stock (undo previous addition)
        foreach (var oldItem in purchase.Items)
        {
            if (oldItem.ProductId.HasValue && productMap.TryGetValue(oldItem.ProductId.Value, out var product))
                product.Stock -= oldItem.Quantity;
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

        // Add new item quantities to stock
        foreach (var newItem in newItems)
        {
            if (newItem.ProductId.HasValue && productMap.TryGetValue(newItem.ProductId.Value, out var product))
                product.Stock += newItem.Quantity;
        }

        // EF change tracker persists changes automatically on CommitAsync
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

        return purchase.Map(x=> new PurchaseDto(
            x.Id, x.SupplierId, x.SupplierName, x.Status, x.Total, x.Notes,
            x.CreatedAt, x.UpdatedAt, x.CreatedBy, x.UpdatedBy,
            x.Items.Select(i => new PurchaseItemDto(
                i.Id, i.ProductId, i.ProductName, i.ProductCode,
                i.Quantity, i.UnitPrice, i.Subtotal
            )).ToList()
        ));
    }

    public async Task DeletePurchaseAsync(int id, int companyId, CancellationToken ct = default)
    {
        var purchase = await unitOfWork.Purchases.GetPurchaseByIdAsync(id, companyId, ct);

        if (purchase is null)
            throw new ArgumentException("Purchase not found");

        await unitOfWork.Purchases.DeletePurchaseAsync(id, ct);
        await unitOfWork.CommitAsync(ct);
    }

    }