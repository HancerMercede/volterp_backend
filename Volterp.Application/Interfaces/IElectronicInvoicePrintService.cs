using EitherWay;
using Volterp.Application.DTOs.ElectronicInvoicePrintDtos;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

public interface IElectronicInvoicePrintService
{
    Task<Either<Error, ElectronicInvoicePrintDto>> PrintAsync(
        Guid invoiceId, Guid companyId, CancellationToken ct = default);
}
