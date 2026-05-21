using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface IAccountingTransactionRepository
{
    Task<PagedResult<AccountingTransaction>> GetAllByCompanyAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<AccountingTransaction?> GetByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<AccountingTransaction> AddAsync(AccountingTransaction entity, CancellationToken ct = default);
    Task UpdateAsync(AccountingTransaction entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}