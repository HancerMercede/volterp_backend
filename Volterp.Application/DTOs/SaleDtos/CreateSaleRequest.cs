using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;

namespace Volterp.Application.DTOs.SaleDtos;

public record CreateSaleRequest(
    Guid? ClienteId,
    string? ClienteName,
    SaleStatus Status,
    decimal Total,
    string? Notes,
    List<CreateSaleItemRequest> Items,
    EcfDocumentType DocumentType = EcfDocumentType.Consumer,
    string? Code = null,
    string? IssuerTaxId = null
) : IMapTo<Sale>
{
    public Sale MapTo()
    {
        // CompanyId is NOT copied here — it is set by SaleService.CreateSaleAsync
        // from the companyId parameter (which the controller reads from the JWT).
        // If you see this method writing CompanyId, that is a regression.
        // Total is NOT copied here — the domain computes Subtotal, Itbis and Total
        // from the items and the tax code of each one.
        // Code IS copied here, but SaleService.CreateSaleAsync owns the final value:
        // when the request leaves it empty the service asks the generator for one.
        return new Sale
        {
            ClienteId = ClienteId,
            ClienteName = ClienteName,
            Status = Status,
            DocumentType = DocumentType,
            Notes = Notes,
            Code = Code ?? string.Empty,
            IssuerTaxId = IssuerTaxId,
            Items = Items.Map(i => i.Project())
        };
    }
}
