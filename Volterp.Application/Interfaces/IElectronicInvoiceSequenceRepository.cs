using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;

namespace Volterp.Application.Interfaces;

public interface IElectronicInvoiceSequenceRepository : IRepositoryBase<ElectronicInvoiceSequence>
{
    Task<int> NextSequenceAsync(Guid companyId, EcfDocumentType documentType, CancellationToken ct = default);
    Task<int> NextSequenceAsync(Guid companyId, string issuerTaxId, EcfDocumentType documentType, CancellationToken ct = default);

    // The ranges of the issuer: the ones with room, the expired ones and the
    // annulled ones. The readiness of the taxpayer is read from them, so it
    // needs to see the ones that cannot be used as well.
    Task<IReadOnlyList<ElectronicInvoiceSequence>> GetRangesAsync(
        Guid companyId, string issuerTaxId, CancellationToken ct = default);
}
