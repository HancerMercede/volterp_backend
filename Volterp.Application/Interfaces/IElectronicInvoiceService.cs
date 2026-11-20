using EitherWay;
using Volterp.Application.DTOs.ElectronicInvoiceDtos;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

public interface IElectronicInvoiceService
{
    // The company travels by hand: the emission runs without an HTTP context,
    // and without one the global tenant filter is off.
    //
    // The document of a completed sale: it is signed and persisted, and it
    // waits in the durable queue for the worker that sends it to the DGII.
    Task<Either<Error, ElectronicInvoiceDto>> IssueAsync(Guid saleId, Guid companyId, CancellationToken ct = default);

    // The read of a single document: the company travels by hand and the
    // document of another company is a 404, not a 403.
    Task<Either<Error, ElectronicInvoiceDto>> GetByIdAsync(
        Guid invoiceId, Guid companyId, CancellationToken ct = default);

    // The explicit retry of a document whose signature failed: it keeps its
    // e-NCF and returns to the queue when the signature succeeds.
    Task<Either<Error, ElectronicInvoiceDto>> RetryAsync(
        Guid invoiceId, Guid companyId, CancellationToken ct = default);

    // The recovery of a document whose answer was lost: the DGII is asked
    // whether it received it instead of sending it again, because a second
    // send of the same e-NCF would burn the number twice.
    Task<Either<Error, ElectronicInvoiceDto>> RecoverAsync(
        Guid companyId, string issuerTaxId, string encf, CancellationToken ct = default);

    // The retrack: the documents whose verdict check is due are asked again
    // and the ones the DGII is still validating get the next appointment with
    // the backoff. It answers how many documents were asked again.
    Task<Either<Error, int>> RetrackAsync(Guid companyId, CancellationToken ct = default);

    // The durable emission queue: signed documents are claimed, sent and
    // scheduled for another attempt when the DGII cannot be reached.
    Task<Either<Error, int>> ProcessPendingAsync(Guid companyId, CancellationToken ct = default);

    // The surface of the fiscal state: the count by state and the open
    // documents of the company. The count and the list come from the same
    // classification, so they cannot disagree.
    Task<Either<Error, ElectronicInvoiceStateSurfaceDto>> GetStateAsync(
        Guid companyId, int limit, CancellationToken ct = default);
}
