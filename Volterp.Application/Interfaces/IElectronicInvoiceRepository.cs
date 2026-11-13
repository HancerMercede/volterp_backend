using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface IElectronicInvoiceRepository : IRepositoryBase<ElectronicInvoice>
{
    /// <summary>
    /// The document issued for a sale. The emitter reads it before issuing to
    /// avoid a second document for a sale that already has one.
    /// </summary>
    Task<ElectronicInvoice?> GetBySaleIdAsync(Guid saleId, CancellationToken ct = default);

    /// <summary>
    /// The document of an e-NCF of an issuer. It is how a lost answer is
    /// recovered: the document is asked for before it is sent again.
    /// </summary>
    Task<ElectronicInvoice?> GetByEncfAsync(Guid companyId, string issuerTaxId, string encf, CancellationToken ct = default);
}
