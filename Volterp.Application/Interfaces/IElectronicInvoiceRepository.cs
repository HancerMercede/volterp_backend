using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface IElectronicInvoiceRepository : IRepositoryBase<ElectronicInvoice>
{
    // The signed documents of a company whose next emission attempt is due.
    // The company travels by hand because the worker runs without HTTP context.
    Task<IReadOnlyList<ElectronicInvoice>> GetPendingForEmissionAsync(
        Guid companyId,
        DateTime now,
        DateTime cutoff,
        int limit,
        CancellationToken ct = default);

    // The companies that have a document waiting in the durable queue.
    Task<IReadOnlyList<Guid>> GetCompanyIdsWithPendingEmissionAsync(
        DateTime now,
        DateTime cutoff,
        int limit,
        CancellationToken ct = default);

    // The claim is conditional so two workers cannot take the same document.
    Task<bool> TryClaimAsync(
        Guid companyId,
        Guid invoiceId,
        string leaseOwner,
        DateTime leaseUntil,
        DateTime now,
        CancellationToken ct = default);

    // The document is read by company and id because the worker has no tenant context.
    Task<ElectronicInvoice?> GetByCompanyAndIdAsync(
        Guid companyId,
        Guid invoiceId,
        CancellationToken ct = default);

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

    /// <summary>
    /// The documents of a company whose verdict check is due. The retrack runs
    /// without an HTTP context, and without one the global tenant filter is
    /// off, so the company travels by hand.
    /// </summary>
    Task<IReadOnlyList<ElectronicInvoice>> GetDueForCheckAsync(Guid companyId, DateTime now, CancellationToken ct = default);

    /// <summary>
    /// The documents of a company that are still open: the ones the tenant
    /// has to act on. The closed verdicts come from the library, so the set
    /// of open documents and the classification of a document cannot
    /// disagree about which verdicts close it.
    /// </summary>
    Task<IReadOnlyList<ElectronicInvoice>> GetOpenAsync(Guid companyId, CancellationToken ct = default);
}
