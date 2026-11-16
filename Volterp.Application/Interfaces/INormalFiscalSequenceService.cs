using EitherWay;
using Volterp.Application.DTOs.NormalFiscalSequenceDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface INormalFiscalSequenceService
{
    Task<PagedResult<NormalFiscalSequenceDto>> GetAllAsync(
        Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default);

    Task<Either<Error, NormalFiscalSequenceDto>> GetByIdAsync(
        Guid id, Guid companyId, CancellationToken ct = default);

    Task<Either<Error, NormalFiscalSequenceDto>> CreateAsync(
        SaveNormalFiscalSequenceDto request, Guid companyId, CancellationToken ct = default);

    Task<Either<Error, NormalFiscalSequenceDto>> UpdateAsync(
        Guid id, SaveNormalFiscalSequenceDto request, Guid companyId, CancellationToken ct = default);
}
