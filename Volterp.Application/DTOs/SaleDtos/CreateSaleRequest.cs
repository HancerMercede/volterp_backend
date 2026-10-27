using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.SaleDtos;

public record CreateSaleRequest(
    Guid? ClienteId,
    string? ClienteName,
    SaleStatus Status,
    decimal Total,
    string? Notes,
    List<CreateSaleItemRequest> Items
) : IMapTo<Sale>
{
    public Sale MapTo()
    {
        // CompanyId is NOT copied here — it is set by SaleService.CreateSaleAsync
        // from the companyId parameter (which the controller reads from the JWT).
        // If you see this method writing CompanyId, that is a regression.
        return new Sale
        {
            ClienteId = ClienteId,
            ClienteName = ClienteName,
            Status = Status,
            Total = Total,
            Notes = Notes,
            Items = Items.Map(i => i.Project())
        };
    }
}