using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class PurchaseRepository(VolterpDbContext context) : RepositoryBase<Purchase>(context), IPurchaseRepository
{
    public async Task<PagedResult<Purchase>> GetAllPurchasesByCompanyAsync(int companyId, int pageNumber, int pageSize,
        CancellationToken ct = default)
        => await GetAllAsync(p => p.CompanyId == companyId, pageNumber, pageSize, ct);

    public async Task<Purchase?> GetPurchaseByIdAsync(int id, int companyId, CancellationToken ct = default)
        => await GetByCondictionsAsync(p => p.Id == id && p.CompanyId == companyId, ct);

    public async Task<Purchase> AddPurchaseAsync(Purchase purchase, CancellationToken ct = default)
    {
        await AddAsync(purchase, ct);
        return purchase;
    }

    public async Task UpdatePurchaseAsync(Purchase purchase, CancellationToken ct = default)
        => await UpdateAsync(purchase, ct);

    public async Task DeletePurchaseAsync(int id, CancellationToken ct = default)
    {
        var purchase = await GetByIdAsync(id, ct);
        if (purchase is not null)
            await DeleteAsync(purchase, ct);
    }
}