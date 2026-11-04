using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;

namespace Volterp.Application.Interfaces;

public interface IElectronicInvoiceSequenceRepository : IRepositoryBase<ElectronicInvoiceSequence>
{
    Task<int> NextSequenceAsync(Guid companyId, EcfDocumentType documentType, CancellationToken ct = default);
}
