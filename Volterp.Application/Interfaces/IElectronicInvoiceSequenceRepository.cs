using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.Interfaces;

public interface IElectronicInvoiceSequenceRepository : IRepositoryBase<ElectronicInvoiceSequence>
{
    Task<int> NextSequenceAsync(Guid companyId, SaleDocumentType documentType, CancellationToken ct = default);
}
