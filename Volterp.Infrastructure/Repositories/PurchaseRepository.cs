using System.Linq.Expressions;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class PurchaseRepository(VolterpDbContext context) : RepositoryBase<Purchase>(context), IPurchaseRepository
{
    public async Task<PagedResult<Purchase>> GetAllPurchasesAsync(int pageNumber, int pageSize,
        CancellationToken ct = default)
        => await GetAllAsync(_ => true, pageNumber, pageSize, ct);

    public async Task<Purchase?> GetPurchaseByIdAsync(Guid id, CancellationToken ct = default)
        => await GetByCondictionsAsync(p => p.Id == id, ct);

    public async Task<Purchase> AddPurchaseAsync(Purchase purchase, CancellationToken ct = default)
    {
        await AddAsync(purchase, ct);
        return purchase;
    }

    public async Task UpdatePurchaseAsync(Purchase purchase, CancellationToken ct = default)
        => await UpdateAsync(purchase, ct);

    public async Task DeletePurchaseAsync(Guid id, CancellationToken ct = default)
    {
        var purchase = await GetByIdAsync(id, ct);
        if (purchase is not null)
            await DeleteAsync(purchase, ct);
    }

    // Exposed by the repository so the application layer can check that the
    // business code is not repeated inside the company without reaching for
    // the DbContext.
    public async Task<bool> ExistsCodeAsync(Guid companyId, string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        if (excludeId.HasValue)
            return await ExistsAsync(p => p.CompanyId == companyId && p.Code == code && p.Id != excludeId.Value, ct);

        return await ExistsAsync(p => p.CompanyId == companyId && p.Code == code, ct);
    }
}
