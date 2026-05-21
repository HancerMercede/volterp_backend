using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class AccountingTransactionRepository(VolterpDbContext context) : RepositoryBase<AccountingTransaction>(context), IAccountingTransactionRepository
{
    public async Task<PagedResult<AccountingTransaction>> GetAllByCompanyAsync(int companyId, int pageNumber, int pageSize,
        CancellationToken ct = default)
        => await GetAllAsync(e => e.CompanyId == companyId, pageNumber, pageSize, ct);

    public async Task<AccountingTransaction?> GetByIdAsync(int id, int companyId, CancellationToken ct = default)
        => await GetByCondictionsAsync(e => e.Id == id && e.CompanyId == companyId, ct);

    public async Task<AccountingTransaction> AddAsync(AccountingTransaction entity, CancellationToken ct = default)
    {
        await AddAsync(entity, ct);
        return entity;
    }

    public async Task UpdateAsync(AccountingTransaction entity, CancellationToken ct = default)
        => await UpdateAsync(entity, ct);

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var transaction = await GetByIdAsync(id, ct);
        if (transaction is not null)
            await DeleteAsync(transaction, ct);
    }
}