using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface ICompanyRepository : IRepositoryBase<Company>
{
    Task<PagedResult<Company>> GetAllCompaniesAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Company?> GetCompanyByIdAsync(int companyId, CancellationToken ct = default);
    Task<Company> AddCompanyAsync(Company company, CancellationToken ct = default);
    Task<Company>  UpdateCompanyAsync(Company company, CancellationToken ct = default);
    Task DeleteCompanyAsync(int companyId, CancellationToken ct = default);
    Task<bool> ExistsCompanyAsync(int companyId, CancellationToken ct = default);
}