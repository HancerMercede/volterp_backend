using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class SaleRepository(VolterpDbContext context) : RepositoryBase<Sale>(context), ISaleRepository
{
    public async Task<Sale?> GetSaleByIdAsync(Guid id, CancellationToken ct = default)
        => await GetByCondictionsAsync(s => s.Id == id,ct,
            s => s
                .Include(i => i.Items)
                .Include(i => i.Company)
                .Include(i => i.ElectronicInvoices));


    public async Task<PagedResult<Sale>> GetAllSalesAsync(int pageNumber, int pageSize,
        CancellationToken ct = default)
    => await GetAllAsync(_ => true, pageNumber, pageSize,ct,
        s=> s.Include(i=>i.Items));


    public async Task<PagedResult<Sale>> GetSalesByStatusAsync(SaleStatus status,
        int pageNumber, int pageSize, CancellationToken ct = default)
    {
        return await GetAllAsync(s => s.Status == status, pageNumber, pageSize, ct);
    }

    public async Task<Sale> AddSaleAsync(Sale sale, CancellationToken ct = default)
    {
        await AddAsync(sale, ct);
        return sale;
    }

    public async Task UpdateSaleAsync(Sale sale, CancellationToken ct = default)
        => await UpdateAsync(sale, ct);

    public async Task DeleteSaleAsync(Guid id, CancellationToken ct = default)
    {
        var sale = await GetByIdAsync(id, ct);
        if (sale is not null)
            await DeleteAsync(sale, ct);
    }

    public async Task<bool> ExistsCodeAsync(Guid companyId, string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        if (excludeId.HasValue)
            return await ExistsAsync(s => s.CompanyId == companyId && s.Code == code && s.Id != excludeId.Value, ct);

        return await ExistsAsync(s => s.CompanyId == companyId && s.Code == code, ct);
    }
}
