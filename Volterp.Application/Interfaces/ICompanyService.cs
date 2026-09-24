using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.CompanyDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface ICompanyService
{
    Task<PagedResult<CompanyDto>>  GetAllCompaniesAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error,CompanyDto?>> GetCompanyByIdAsync(Guid id, CancellationToken ct = default);
    Task<Either<Error,CompanyDto?>> AddCompanyAsync(CreateCompanyDto company, Guid? userId, CancellationToken ct = default);

    Task<Either<Error,CompanyDto?>> UpdateCompanyAsync(Guid companyId, UpdateCompanyDto company, Guid? userId, CancellationToken ct = default);

    Task<Either<Error,Unit>> DeleteCompanyAsync(Guid id, CancellationToken ct = default);

    Task<Either<Error,bool>> ExistsCompanyAsync(Guid id, CancellationToken ct = default);
}
