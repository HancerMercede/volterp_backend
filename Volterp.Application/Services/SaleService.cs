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
                new SaleItemDto(i.Id, i.ProductId, i.ProductName, i.ProductCategory, i.ProductCode, i.ProductImageUrl, i.Quantity, i.UnitPrice, i.Subtotal))
                .ToList()
        ));
    }

    public async Task<PagedResult<SaleDto>> GetSalesByStatusAsync(int companyId, SaleStatus status, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var sales = await unitOfWork.Sales.GetSalesByStatusAsync(companyId, status, pageNumber, pageSize, ct);
        
        return sales.Map(s => new SaleDto(
            s.Id, s.CompanyId, s.ClienteId, s.ClienteName, s.Status, s.Total, s.Notes, 
            s.CreatedAt, s.UpdatedAt,
            s.Items.Select(i => new SaleItemDto(i.Id, i.ProductId, i.ProductName, i.ProductCategory, i.ProductCode, i.ProductImageUrl, i.Quantity, i.UnitPrice, i.Subtotal)).ToList()
        ));
    }

    public async Task<SaleDto?> GetSaleByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        var sale = await unitOfWork.Sales.GetSaleByIdAsync(id, companyId, ct);
        
        if (sale is null) return null;
        
        return sale.Map(s => new SaleDto(
            s.Id, s.CompanyId, s.ClienteId, s.ClienteName, s.Status, s.Total, s.Notes,
            s.CreatedAt, s.UpdatedAt,
            s.Items.Select(i => new SaleItemDto(i.Id, i.ProductId, i.ProductName, i.ProductCategory, i.ProductCode, i.ProductImageUrl, i.Quantity, i.UnitPrice, i.Subtotal)).ToList()
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

        await unitOfWork.Sales.AddSaleAsync(sale, ct);
        await unitOfWork.CommitAsync(ct);

        return sale.Map(s => new SaleDto(
            s.Id, s.CompanyId, s.ClienteId, s.ClienteName, s.Status, s.Total, s.Notes,
            s.CreatedAt, s.UpdatedAt,
            s.Items.Select(i => new SaleItemDto(i.Id, i.ProductId, i.ProductName, i.ProductCategory, i.ProductCode, i.ProductImageUrl, i.Quantity, i.UnitPrice, i.Subtotal)).ToList()
        ));
    }

public async Task<SaleDto> UpdateSaleAsync(int id, int companyId, UpdateSaleRequest request, CancellationToken ct = default)
    {
        var sale = await unitOfWork.Sales.GetSaleByIdAsync(id, companyId, ct);
        
        if (sale is null)
            throw new ArgumentException("Sale not found");

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
            s.Items.Select(i => new SaleItemDto(i.Id, i.ProductId, i.ProductName, i.ProductCategory, i.ProductCode, i.ProductImageUrl, i.Quantity, i.UnitPrice, i.Subtotal)).ToList()
        ));
    }

    public async Task<SaleDto> CompleteSaleAsync(int id, int companyId, CancellationToken ct = default)
    {
        var sale = await unitOfWork.Sales.GetSaleByIdAsync(id, companyId, ct);
        
        if (sale is null)
            throw new ArgumentException("Sale not found");

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
            s.Items.Select(i => new SaleItemDto(i.Id, i.ProductId, i.ProductName, i.ProductCategory, i.ProductCode, i.ProductImageUrl, i.Quantity, i.UnitPrice, i.Subtotal)).ToList()
        ));
    }

    public async Task DeleteSaleAsync(int id, int companyId, CancellationToken ct = default)
    {
        var sale = await unitOfWork.Sales.GetSaleByIdAsync(id, companyId, ct);
        
        if (sale is null)
            throw new ArgumentException("Sale not found");

        await unitOfWork.Sales.DeleteSaleAsync(id, ct);
        await unitOfWork.CommitAsync(ct);
    }
}