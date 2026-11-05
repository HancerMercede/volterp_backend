using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class AccountingTransactionRepository(VolterpDbContext context) :
    RepositoryBase<AccountingTransaction>(context),
    IAccountingTransactionRepository
{
    public async Task<PagedResult<AccountingTransaction>> GetAllAsync(int pageNumber, int pageSize,
        CancellationToken ct = default)
        => await GetAllAsync(_ => true, pageNumber, pageSize, ct);

    public async Task<AccountingTransaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await GetByCondictionsAsync(e => e.Id == id, ct);

    public async Task<AccountingTransaction> AddTransactionAsync(AccountingTransaction entity, CancellationToken ct = default)
    {
        await AddAsync(entity, ct);
        return entity;
    }

    public async Task UpdateTransactionAsync(AccountingTransaction entity, CancellationToken ct = default)
        => await UpdateAsync(entity, ct);

    public async Task DeleteTransactionAsync(Guid id, CancellationToken ct = default)
    {
        var transaction = await GetByIdAsync(id, ct);
        if (transaction is not null)
            await DeleteAsync(transaction, ct);
    }

    public async Task<bool> ExistsCodeAsync(Guid companyId, string code, Guid? excludeId = null,
        CancellationToken ct = default)
    {
        if (excludeId.HasValue)
            return await ExistsAsync(t => t.CompanyId == companyId && t.Code == code && t.Id != excludeId.Value, ct);

        return await ExistsAsync(t => t.CompanyId == companyId && t.Code == code, ct);
    }
}
