using EitherWay;
using Volterp.Application.DTOs.CompanyDtos.EmissionMode;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

public interface ICompanyEmissionModeService
{
    Task<Either<Error, CompanyEmissionModeDto>> GetAsync(
        Guid companyId, CancellationToken ct = default);

    Task<Either<Error, CompanyEmissionModeDto>> UpdateAsync(
        UpdateCompanyEmissionModeDto request, Guid companyId, Guid? userId = null, CancellationToken ct = default);

    Task<Either<Error, CompanyEmissionModeDto>> UpdateContingencyAsync(
        UpdateCompanyContingencyDto request, Guid companyId, Guid? userId = null, CancellationToken ct = default);
}
