namespace Volterp.Application.DTOs.SaleDtos;

public record SaleItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string? ProductCategory,
    string? ProductCode,
    string? ProductImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal
);