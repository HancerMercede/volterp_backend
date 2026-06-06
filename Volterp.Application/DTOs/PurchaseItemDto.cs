namespace Volterp.Application.DTOs;

public record PurchaseItemDto(
    int Id,
    int? ProductId,
    string ProductName,
    string ProductCode,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal
);