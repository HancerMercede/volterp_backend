using Volterp.Application.DTOs;
using Volterp.Application.DTOs.CompanyDtos;
using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface ICompanyService
{
    Task<PagedResult<CompanyDto>>  GetAllCompaniesAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<CompanyDto?> GetCompanyByIdAsync(int id, CancellationToken ct = default);
    Task<CompanyDto?> AddCompanyAsync(CreateCompanyDto company, CancellationToken ct = default);
    
    Task<CompanyDto?> UpdateCompanyAsync(int companyId, UpdateCompanyDto company, CancellationToken ct = default);
    
    Task DeleteCompanyAsync(int id, CancellationToken ct = default);
    
    Task<bool> ExistsCompanyAsync(int id, CancellationToken ct = default);
}