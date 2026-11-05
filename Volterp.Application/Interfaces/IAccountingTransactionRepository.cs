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

    /// <summary>
    /// Tells whether another accounting transaction of the same company already uses
    /// <paramref name="code"/>. <paramref name="excludeId"/> skips a row by id so an
    /// entity can keep its own code on update.
    /// </summary>
    Task<bool> ExistsCodeAsync(Guid companyId, string code, Guid? excludeId = null, CancellationToken ct = default);
}
