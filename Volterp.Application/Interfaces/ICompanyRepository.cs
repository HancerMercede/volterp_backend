using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface ICompanyRepository : IRepositoryBase<Company>
{
    Task<Company?> GetCompanyByIdAsync(int id, CancellationToken ct = default);
    Task<Company> AddCompanyAsync(Company company, CancellationToken ct = default);
    Task<bool> ExistsCompanyAsync(int id, CancellationToken ct = default);
}