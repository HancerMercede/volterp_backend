using EitherWay;
using Volterp.Application.DTOs.ReceivedElectronicInvoiceDtos;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

public interface IElectronicInvoiceReceptionService
{
    Task<Either<Error, ReceivedElectronicInvoiceDto>> ReceiveAsync(
        string xml, string fileName, CancellationToken ct = default);

    Task<Either<Error, ReceivedElectronicInvoiceDto>> ReceiveCommercialApprovalAsync(
        string xml, string fileName, CancellationToken ct = default);

    // The document that does not exist is the absence of the Right channel:
    // the caller tells it apart from a failure without reading the message.
    Task<Either<Error, ReceivedElectronicInvoiceDto?>> DecideCommercialApprovalAsync(
        Guid id, Guid companyId, Guid userId, bool approved, string reason, CancellationToken ct = default);
}
