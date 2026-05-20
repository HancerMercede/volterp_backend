namespace Volterp.Application.DTOs;

public record PurchaseDto(
    int Id,
    int? SupplierId,
    string SupplierName,
    string Status,
    decimal Total,
    string Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int? CreatedBy,
    int? UpdatedBy,
    List<PurchaseItemDto> Items
);