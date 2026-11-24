using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;

namespace Volterp.Application.DTOs.SaleDtos;

public record UpdateSaleRequest(
    Guid? ClienteId,
    string? ClienteName,
    SaleStatus Status,
    decimal Total,
    string? Notes,
    List<CreateSaleItemRequest> Items,
    EcfDocumentType DocumentType = EcfDocumentType.Consumer,
    string? Code = null,
    string? IssuerTaxId = null,
    string? ReceptorTaxId = null
);
