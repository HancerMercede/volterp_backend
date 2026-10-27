using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.SaleDtos;

public record CreateSaleItemRequest(
Guid ProductId,
string ProductName,
string? ProductCategory,
string? ProductCode,
string? ProductImageUrl,
int Quantity,
decimal UnitPrice,
decimal Subtotal
) : IMapTo<SaleItem>
{
    public SaleItem MapTo()
    {
        return new SaleItem
        {
            ProductId = ProductId,
            ProductName = ProductName,
            ProductCategory = ProductCategory,
            ProductCode = ProductCode,
            ProductImageUrl = ProductImageUrl,
            Quantity = Quantity,
            UnitPrice = UnitPrice,
            Subtotal = Subtotal
        };
    }
}