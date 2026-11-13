using EitherWay;
using Volterp.Application.DTOs.ElectronicInvoiceDtos;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

public interface IElectronicInvoiceService
{
    // The company travels by hand: the emission runs without an HTTP context,
    // and without one the global tenant filter is off.
    Task<Either<Error, ElectronicInvoiceDto>> IssueAsync(Guid saleId, Guid companyId, CancellationToken ct = default);

    // The recovery of a document whose answer was lost: the DGII is asked
    // whether it received it instead of sending it again, because a second
    // send of the same e-NCF would burn the number twice.
    Task<Either<Error, ElectronicInvoiceDto>> RecoverAsync(
        Guid companyId, string issuerTaxId, string encf, CancellationToken ct = default);

    // The retrack: the documents whose verdict check is due are asked again
    // and the ones the DGII is still validating get the next appointment with
    // the backoff. It answers how many documents were asked again.
    Task<Either<Error, int>> RetrackAsync(Guid companyId, CancellationToken ct = default);
}
