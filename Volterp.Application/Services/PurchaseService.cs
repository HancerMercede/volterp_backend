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

        return new PurchaseDto(
            purchase.Id, purchase.SupplierId, purchase.SupplierName, purchase.Status, purchase.Total, purchase.Notes,
            purchase.CreatedAt, purchase.UpdatedAt, purchase.CreatedBy, purchase.UpdatedBy,
            purchase.Items.Select(i => new PurchaseItemDto(
                i.Id, i.ProductId, i.ProductName, i.ProductCode,
                i.Quantity, i.UnitPrice, i.Subtotal
            )).ToList()
        );
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

        await unitOfWork.Purchases.AddPurchaseAsync(purchase, ct);
        await unitOfWork.CommitAsync(ct);

        return new PurchaseDto(
            purchase.Id, purchase.SupplierId, purchase.SupplierName, purchase.Status, purchase.Total, purchase.Notes,
            purchase.CreatedAt, purchase.UpdatedAt, purchase.CreatedBy, purchase.UpdatedBy,
            purchase.Items.Select(i => new PurchaseItemDto(
                i.Id, i.ProductId, i.ProductName, i.ProductCode,
                i.Quantity, i.UnitPrice, i.Subtotal
            )).ToList()
        );
    }

    public async Task<PurchaseDto> UpdatePurchaseAsync(int id, int companyId, PurchaseDto request, int? userId, CancellationToken ct = default)
    {
        var purchase = await unitOfWork.Purchases.GetPurchaseByIdAsync(id, companyId, ct);

        if (purchase is null)
            throw new ArgumentException("Purchase not found");

        purchase.SupplierId = request.SupplierId;
        purchase.SupplierName = request.SupplierName;
        purchase.Status = request.Status;
        purchase.Total = request.Total;
        purchase.Notes = request.Notes;
        purchase.UpdatedAt = DateTime.UtcNow;
        purchase.UpdatedBy = userId;

        purchase.Items.Clear();
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

        await unitOfWork.Purchases.UpdatePurchaseAsync(purchase, ct);
        await unitOfWork.CommitAsync(ct);

        return new PurchaseDto(
            purchase.Id, purchase.SupplierId, purchase.SupplierName, purchase.Status, purchase.Total, purchase.Notes,
            purchase.CreatedAt, purchase.UpdatedAt, purchase.CreatedBy, purchase.UpdatedBy,
            purchase.Items.Select(i => new PurchaseItemDto(
                i.Id, i.ProductId, i.ProductName, i.ProductCode,
                i.Quantity, i.UnitPrice, i.Subtotal
            )).ToList()
        );
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