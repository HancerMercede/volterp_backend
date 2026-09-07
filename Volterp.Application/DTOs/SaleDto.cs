using System.Collections.Immutable;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs;

public record SaleDto:IMapFrom<Sale>
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int? ClienteId { get; set; }
    public string? ClienteName { get; set; }
    public SaleStatus Status { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<SaleItemDto> Items { get; set; }

    public void MapFrom(Sale source)
    {
        Id = source.Id;
        CompanyId = source.CompanyId;
        ClienteId = source.ClienteId;
        ClienteName = source.ClienteName;
        Status = source.Status;
        Total = source.Total;
        Notes = source.Notes;
        CreatedAt = source.CreatedAt;
        UpdatedAt = source.UpdatedAt;
        Items = MapToSaleItemDto(source.Items.ToList());
    }

    private List<SaleItemDto> MapToSaleItemDto(List<SaleItem> sources)
    {
        return sources.Map(x => 
            new SaleItemDto(
                x.Id,
                x.ProductId, 
                x.ProductName,
                x.ProductCategory,
                x.ProductCode,
                x.ProductImageUrl,
                x.Quantity,
                x.UnitPrice,
                x.Subtotal));
    }
}

public record SaleItemDto(
    int Id,
    int ProductId,
    string ProductName,
    string? ProductCategory,
    string? ProductCode,
    string? ProductImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal
);

public record CreateSaleRequest(
    int CompanyId,
    int? ClienteId,
    string? ClienteName,
    SaleStatus Status,
    decimal Total,
    string? Notes,
    List<CreateSaleItemRequest> Items
):IMapTo<Sale>
{
    public Sale MapTo()
    {
        return new Sale
        {
           CompanyId = CompanyId,
           ClienteId = ClienteId,
           ClienteName = ClienteName,
           Status = Status,
           Total = Total,
           Notes = Notes,
           Items = Items.Map(x=> new SaleItem
           {
            ProductId  = x.ProductId,
            ProductName = x.ProductName,
            ProductCategory = x.ProductCategory,
            ProductCode = x.ProductCode,
            ProductImageUrl = x.ProductImageUrl,
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice,
            Subtotal = x.Subtotal
           })
        };
    }
}

public record CreateSaleItemRequest(
    int ProductId,
    string ProductName,
    string? ProductCategory,
    string? ProductCode,
    string? ProductImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal
);

public record UpdateSaleRequest(
    int? ClienteId,
    string? ClienteName,
    SaleStatus Status,
    decimal Total,
    string? Notes,
    List<CreateSaleItemRequest> Items
);