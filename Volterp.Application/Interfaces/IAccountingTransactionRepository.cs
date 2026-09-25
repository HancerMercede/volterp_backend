using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface IAccountingTransactionRepository
{
    Task<PagedResult<AccountingTransaction>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<AccountingTransaction?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AccountingTransaction> AddTransactionAsync(AccountingTransaction entity, CancellationToken ct = default);
    Task UpdateTransactionAsync(AccountingTransaction entity, CancellationToken ct = default);
    Task DeleteTransactionAsync(Guid id, CancellationToken ct = default);
}
