using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface INormalFiscalDocumentRepository : IRepositoryBase<NormalFiscalDocument>
{
    // The document of a sale. It is read before issuing so a sale that
    // already has one does not consume another number.
    Task<NormalFiscalDocument?> GetBySaleIdAsync(Guid companyId, Guid saleId, CancellationToken ct = default);
}
