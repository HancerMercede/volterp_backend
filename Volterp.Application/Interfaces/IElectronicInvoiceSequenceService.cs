using Volterp.Application.DTOs.ElectronicInvoiceSequenceDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using EitherWay;

namespace Volterp.Application.Interfaces;

public interface IElectronicInvoiceSequenceService
{
    Task<PagedResult<ElectronicInvoiceSequenceDto>> GetAllAsync(
        Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default);

    Task<Either<Error, ElectronicInvoiceSequenceDto>> GetByIdAsync(
        Guid id, Guid companyId, CancellationToken ct = default);

    Task<Either<Error, ElectronicInvoiceSequenceDto>> CreateAsync(
        SaveElectronicInvoiceSequenceDto request, Guid companyId, CancellationToken ct = default);

    Task<Either<Error, ElectronicInvoiceSequenceDto>> UpdateAsync(
        Guid id, SaveElectronicInvoiceSequenceDto request, Guid companyId, CancellationToken ct = default);
}
