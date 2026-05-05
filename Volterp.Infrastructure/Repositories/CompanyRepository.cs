using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class CompanyRepository(VolterpDbContext context)
    : RepositoryBase<Company>(context), ICompanyRepository
{
    public async Task<Company?> GetCompanyByIdAsync(int id, CancellationToken ct = default)
        => await Set().SingleOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Company> AddCompanyAsync(Company company, CancellationToken ct = default)
    {
        await AddAsync(company, ct);
        return company;
    }

    public async Task<bool> ExistsCompanyAsync(int id, CancellationToken ct = default)
        => await ExistsAsync(c => c.Id == id, ct);
}