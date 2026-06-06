using Microsoft.EntityFrameworkCore;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class SaleRepository(VolterpDbContext context) : RepositoryBase<Sale>(context), ISaleRepository
{
    public async Task<Sale?> GetSaleByIdAsync(int id, int companyId, CancellationToken ct = default)
        => await GetByCondictionsAsync(s => s.Id == id && s.CompanyId == companyId,ct,
            s => s.Include(i => i.Items));
     

    public async Task<PagedResult<Sale>> GetAllSalesByCompanyAsync(int companyId, int pageNumber, int pageSize,
        CancellationToken ct = default)
    => await GetAllAsync(s => s.CompanyId == companyId, pageNumber, pageSize,ct,
        s=> s.Include(i=>i.Items));


    public async Task<PagedResult<Sale>> GetSalesByStatusAsync(int companyId, SaleStatus status, 
        int pageNumber, int pageSize, CancellationToken ct = default)
    {
        return await GetAllAsync(s => s.CompanyId == companyId 
                                      && s.Status == status, pageNumber, pageSize, ct);
    }

    public async Task<Sale> AddSaleAsync(Sale sale, CancellationToken ct = default)
    {
        await AddAsync(sale, ct);
        return sale;
    }

    public async Task UpdateSaleAsync(Sale sale, CancellationToken ct = default)
        => await UpdateAsync(sale, ct);

    public async Task DeleteSaleAsync(int id, CancellationToken ct = default)
    {
        var sale = await GetByIdAsync(id, ct);
        if (sale is not null)
            await DeleteAsync(sale, ct);
    }
}