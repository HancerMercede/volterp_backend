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
    Task<Either<Error,CompanyDto?>> GetCompanyByIdAsync(int id, CancellationToken ct = default);
    Task<Either<Error,CompanyDto?>> AddCompanyAsync(CreateCompanyDto company, CancellationToken ct = default);
    
    Task<Either<Error,CompanyDto?>> UpdateCompanyAsync(int companyId, UpdateCompanyDto company, CancellationToken ct = default);
    
    Task<Either<Error,Unit>> DeleteCompanyAsync(int id, CancellationToken ct = default);
    
    Task<Either<Error,bool>> ExistsCompanyAsync(int id, CancellationToken ct = default);
}