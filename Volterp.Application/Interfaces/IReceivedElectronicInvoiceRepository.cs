using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface IReceivedElectronicInvoiceRepository : IRepositoryBase<ReceivedElectronicInvoice>
{
    Task<ReceivedElectronicInvoice?> GetByIssuerAndEncfAsync(
        string issuerTaxId,
        string encf,
        CancellationToken ct = default);

    Task<ReceivedElectronicInvoice?> GetByCompanyAndIdAsync(
        Guid companyId,
        Guid id,
        CancellationToken ct = default);
}
