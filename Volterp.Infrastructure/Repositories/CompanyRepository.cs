using Microsoft.EntityFrameworkCore;
using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class CompanyRepository(VolterpDbContext context)
    : RepositoryBase<Company>(context), ICompanyRepository
{
    public async Task<PagedResult<Company>> GetAllCompaniesAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var companies = await GetAllAsync(c => c.IsActive == true, pageNumber, pageSize, ct);
        return companies;
    }

    public async Task<Company?> GetCompanyByIdAsync(int companyId, CancellationToken ct = default)
        => await GetByCondictionsAsync(c => c.Id == companyId, ct);

    public async Task<Company> AddCompanyAsync(Company company, CancellationToken ct = default)
    {
        await AddAsync(company, ct);
        return company;
    }

    public async Task<Company> UpdateCompanyAsync(Company company, CancellationToken ct = default)
    {
        await UpdateAsync(company, ct);
        return company;
    }

    public async Task DeleteCompanyAsync(int companyId, CancellationToken ct = default)
    {
        var company = await GetByIdAsync(companyId, ct);
        await DeleteAsync(company, ct);
    }
    
    public async Task<bool> ExistsCompanyAsync(int companyId, CancellationToken ct = default)
        => await ExistsAsync(c => c.Id == companyId, ct);
}