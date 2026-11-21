using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.Interfaces;

public interface INormalFiscalSequenceRepository : IRepositoryBase<NormalFiscalSequence>
{
    // The next number of the range the DGII authorized for the normal
    // documents of the issuer. The allocation is one statement, it is locked
    // by row and it never leaves the range.
    Task<int> NextSequenceAsync(
        Guid companyId,
        string issuerTaxId,
        NormalFiscalDocumentType documentType,
        CancellationToken ct = default);

    // The ranges of the issuer: the ones with room and the ones that cannot
    // be used anymore. The readiness of the taxpayer is read from them, so
    // it needs to see the expired ones as well.
    Task<IReadOnlyList<NormalFiscalSequence>> GetRangesAsync(
        Guid companyId, string issuerTaxId, CancellationToken ct = default);
}
