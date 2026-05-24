using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.Services;

public class SaleService(IUnitOfWork unitOfWork) : ISaleService
{
    public async Task<PagedResult<SaleDto>> GetAllSalesAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var sales = await unitOfWork.Sales.GetAllSalesByCompanyAsync(companyId, pageNumber, pageSize, ct);
        
        return sales.Map(s => new SaleDto(
            s.Id, s.CompanyId, s.ClienteId, s.ClienteName, s.Status, s.Total, s.Notes, 
            s.CreatedAt, s.UpdatedAt,
            s.Items.Select(i => 
                new SaleItemDto(i.Id, i.ProductId, i.ProductName, i.ProductCategory,
                    i.ProductCode, i.ProductImageUrl, i.Quantity, i.UnitPrice, i.Subtotal))
                .ToList()
        ));
    }

    public async Task<PagedResult<SaleDto>> GetSalesByStatusAsync(int companyId, SaleStatus status, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var sales = await unitOfWork.Sales.GetSalesByStatusAsync(companyId, status, pageNumber, pageSize, ct);
        
        return sales.Map(s => new SaleDto(
            s.Id, s.CompanyId, s.ClienteId, s.ClienteName, s.Status, s.Total, s.Notes, 
            s.CreatedAt, s.UpdatedAt,
            s.Items.Select(i => new SaleItemDto(i.Id, i.ProductId, i.ProductName, 
                i.ProductCategory, i.ProductCode, i.ProductImageUrl, i.Quantity,
                i.UnitPrice, i.Subtotal))
                .ToList()
        ));
    }

    public async Task<SaleDto?> GetSaleByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        var sale = await unitOfWork.Sales.GetSaleByIdAsync(id, companyId, ct);
        
        if (sale is null) return null;
        
        return sale.Map(s => new SaleDto(
            s.Id, s.CompanyId, s.ClienteId, s.ClienteName, s.Status, s.Total, s.Notes,
            s.CreatedAt, s.UpdatedAt,
            s.Items.Select(i => new SaleItemDto(i.Id, 
                i.ProductId, i.ProductName, i.ProductCategory, 
                i.ProductCode, i.ProductImageUrl, 
                i.Quantity, i.UnitPrice, i.Subtotal))
                .ToList()
        ));
    }

public async Task<SaleDto> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct = default)
    {
        var sale = request.Map(r => new Sale
        {
            CompanyId = r.CompanyId,
            ClienteId = r.ClienteId,
            ClienteName = r.ClienteName,
            Status = r.Status,
            Total = r.Total,
            Notes = r.Notes,
            CreatedAt = DateTime.UtcNow,
            Items = r.Items.Select(i => new SaleItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                ProductCategory = i.ProductCategory,
                ProductCode = i.ProductCode,
                ProductImageUrl = i.ProductImageUrl,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Subtotal = i.Subtotal
            }).ToList()
        });

        // Batch query: single SELECT for all products (avoids N+1)
        var productIds = sale.Items.Select(i => i.ProductId).ToHashSet();
        var products = await unitOfWork.Products.GetProductsByIdsAsync(productIds, ct);
        var productMap = products.ToDictionary(p => p.Id);

        // Compute net deductions per product in one pass
        var deductions = sale.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        // Build product name lookup (use FirstOrDefault to handle duplicate ProductIds gracefully)
        var itemNameMap = sale.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.First().ProductName);

        foreach (var (productId, quantity) in deductions)
        {
            if (!productMap.TryGetValue(productId, out var product))
                continue;

            if (product.Stock < quantity)
            {
                var itemName = itemNameMap.GetValueOrDefault(productId, productId.ToString());
                throw new InvalidOperationException(
                    $"Insufficient stock for product '{itemName}'. Available: {product.Stock}, Requested: {quantity}");
            }
            product.Stock -= quantity;
        }

        // EF change tracker persists changes automatically on CommitAsync
        await unitOfWork.Sales.AddSaleAsync(sale, ct);
        await unitOfWork.CommitAsync(ct);

        return sale.Map(s => new SaleDto(
            s.Id, s.CompanyId, s.ClienteId, s.ClienteName, s.Status, s.Total, s.Notes,
            s.CreatedAt, s.UpdatedAt,
            s.Items.Select(i => new SaleItemDto(i.Id, i.ProductId, i.ProductName, 
                i.ProductCategory, i.ProductCode, i.ProductImageUrl, 
                i.Quantity, i.UnitPrice, i.Subtotal))
                .ToList()
        ));
    }

    public async Task<SaleDto> UpdateSaleAsync(int id, int companyId, UpdateSaleRequest request, CancellationToken ct = default)
    {
        var sale = await unitOfWork.Sales.GetSaleByIdAsync(id, companyId, ct);
        
        if (sale is null)
            throw new ArgumentException("Sale not found");

        // Collect all product IDs (old items + new items)
        var allProductIds = sale.Items.Select(i => i.ProductId)
            .Concat(request.Items.Select(i => i.ProductId))
            .ToHashSet();

        // Batch query: single SELECT for all products involved
        var products = await unitOfWork.Products.GetProductsByIdsAsync(allProductIds, ct);
        var productMap = products.ToDictionary(p => p.Id);

        // Compute net stock changes: old items returned (+), new items deducted (-)
        var oldReturns = sale.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var newDeductions = request.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        // Apply old returns
        foreach (var (productId, quantity) in oldReturns)
        {
            if (productMap.TryGetValue(productId, out var product))
                product.Stock += quantity;
        }

        // Build product name lookup (O(n) once, avoids O(n²) First() in loop)
        var itemNameMap = request.Items
            .ToDictionary(i => i.ProductId, i => i.ProductName);

        // Validate and apply new deductions
        foreach (var (productId, quantity) in newDeductions)
        {
            if (!productMap.TryGetValue(productId, out var product))
                continue;

            if (product.Stock < quantity)
            {
                var itemName = itemNameMap.GetValueOrDefault(productId, productId.ToString());
                throw new InvalidOperationException(
                    $"Insufficient stock for product '{itemName}'. Available: {product.Stock}, Requested: {quantity}");
            }
            product.Stock -= quantity;
        }

        var newItems = request.Items.Select(i => new SaleItem
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

        // EF change tracker persists changes automatically on CommitAsync

        sale.Apply(s =>
        {
            s.ClienteId = request.ClienteId;
            s.ClienteName = request.ClienteName;
            s.Status = request.Status;
            s.Total = request.Total;
            s.Notes = request.Notes;
            s.UpdatedAt = DateTime.UtcNow;
            s.Items = newItems;
        });

        await unitOfWork.Sales.UpdateSaleAsync(sale, ct);
        await unitOfWork.CommitAsync(ct);

        return sale.Map(s => new SaleDto(
            s.Id, s.CompanyId, s.ClienteId, s.ClienteName, s.Status, s.Total, s.Notes,
            s.CreatedAt, s.UpdatedAt,
            s.Items.Select(i => new SaleItemDto(i.Id, i.ProductId, i.ProductName,
                i.ProductCategory, i.ProductCode, i.ProductImageUrl, 
                i.Quantity, i.UnitPrice, i.Subtotal))
                .ToList()
        ));
    }

    public async Task<SaleDto> CompleteSaleAsync(int id, int companyId, CancellationToken ct = default)
    {
        var sale = await unitOfWork.Sales.GetSaleByIdAsync(id, companyId, ct);
        
        if (sale is null)
            throw new ArgumentException("Sale not found");

        // Only validate stock if there are items with product IDs and Products is available
        var productIds = sale.Items.Select(i => i.ProductId).ToHashSet();
        if (productIds.Count > 0 && unitOfWork.Products != null)
        {
            // Batch query: re-validate stock before completing
            var products = await unitOfWork.Products.GetProductsByIdsAsync(productIds, ct);
            if (products != null)
            {
                var productMap = products.ToDictionary(p => p.Id);

// Build product name lookup once (avoids O(n²) FirstOrDefault in loop)
                var itemNameMap = sale.Items.ToDictionary(i => i.ProductId, i => i.ProductName);

                // Compute required quantities per product
                var requiredQuantities = sale.Items
                    .GroupBy(i => i.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

                // Validate stock for each product
                foreach (var (productId, quantity) in requiredQuantities)
                {
                    if (!productMap.TryGetValue(productId, out var product))
                        continue;

                    if (product.Stock < quantity)
                    {
                        var itemName = itemNameMap.GetValueOrDefault(productId, productId.ToString());
                        throw new InvalidOperationException(
                            $"Cannot complete sale. Insufficient stock for product '{itemName}'. Available: {product.Stock}, Requested: {quantity}");
                    }
                }
            }
        }

        sale.Apply(s =>
        {
            s.Status = SaleStatus.Completed;
            s.UpdatedAt = DateTime.UtcNow;
        });

        await unitOfWork.Sales.UpdateSaleAsync(sale, ct);
        await unitOfWork.CommitAsync(ct);

        return sale.Map(s => new SaleDto(
            s.Id, s.CompanyId, s.ClienteId, s.ClienteName, s.Status, s.Total, s.Notes,
            s.CreatedAt, s.UpdatedAt,
            s.Items.Select(i => new SaleItemDto(i.Id, i.ProductId, i.ProductName,
                i.ProductCategory, i.ProductCode, i.ProductImageUrl, 
                i.Quantity, i.UnitPrice, i.Subtotal))
                .ToList()
        ));
    }

    public async Task DeleteSaleAsync(int id, int companyId, CancellationToken ct = default)
    {
        var sale = await unitOfWork.Sales.GetSaleByIdAsync(id, companyId, ct);
        
        if (sale is null)
            throw new ArgumentException("Sale not found");

        // Only restore stock if there are items with product IDs
        var productIds = sale.Items.Select(i => i.ProductId).ToHashSet();
        if (productIds.Count > 0)
        {
            // Batch query: restore stock for all items before deleting
            var products = await unitOfWork.Products.GetProductsByIdsAsync(productIds, ct);
            var productMap = products.ToDictionary(p => p.Id);

            // Restore stock for each item
            foreach (var item in sale.Items)
            {
                if (productMap.TryGetValue(item.ProductId, out var product))
                    product.Stock += item.Quantity;
            }
        }

        await unitOfWork.Sales.DeleteSaleAsync(id, ct);
        await unitOfWork.CommitAsync(ct);
    }
}