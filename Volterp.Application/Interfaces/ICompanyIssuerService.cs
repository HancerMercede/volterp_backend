using EitherWay;
using Volterp.Application.DTOs.CompanyIssuerDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface ICompanyIssuerService
{
    Task<PagedResult<CompanyIssuerDto>> GetAllAsync(
        Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default);

    Task<Either<Error, CompanyIssuerDto>> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);

    Task<Either<Error, CompanyIssuerDto>> CreateAsync(
        CreateCompanyIssuerRequest request, Guid companyId, Guid? userId, CancellationToken ct = default);

    Task<Either<Error, CompanyIssuerDto>> UpdateAsync(
        Guid id, UpdateCompanyIssuerRequest request, Guid companyId, Guid? userId, CancellationToken ct = default);
}
