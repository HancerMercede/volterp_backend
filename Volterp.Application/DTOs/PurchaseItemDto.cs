using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs;

public record PurchaseItemDto : IMapFrom<PurchaseItem>
{
    public int Id { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }

    public void MapFrom(PurchaseItem source)
    {
        Id = source.Id;
        ProductId = source.ProductId;
        ProductName = source.ProductName;
        ProductCode = source.ProductCode;
        Quantity = source.Quantity;
        UnitPrice = source.UnitPrice;
        Subtotal = source.Subtotal;
    }
}
