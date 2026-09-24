using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class AccountingTransactionRepository(VolterpDbContext context) :
    RepositoryBase<AccountingTransaction>(context),
    IAccountingTransactionRepository
{
    public async Task<PagedResult<AccountingTransaction>> GetAllByCompanyAsync(Guid companyId, int pageNumber, int pageSize,
        CancellationToken ct = default)
        => await GetAllAsync(_ => true, pageNumber, pageSize, ct);

    public async Task<AccountingTransaction?> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default)
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
}
