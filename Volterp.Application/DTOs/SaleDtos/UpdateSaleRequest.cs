using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.SaleDtos;

public record UpdateSaleRequest(
    Guid? ClienteId,
    string? ClienteName,
    SaleStatus Status,
    decimal Total,
    string? Notes,
    List<CreateSaleItemRequest> Items
);