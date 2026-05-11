using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs;

public record SaleDto(
    int Id,
    int CompanyId,
    int? ClienteId,
    string? ClienteName,
    SaleStatus Status,
    decimal Total,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<SaleItemDto> Items
);

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
);

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