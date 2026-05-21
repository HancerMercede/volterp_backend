using Volterp.Application.DTOs;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IAccountingTransactionService
{
    Task<PagedResult<AccountingTransactionDto>> GetAllByCompanyAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<AccountingTransactionDto?> GetByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<AccountingTransactionDto> CreateAsync(AccountingTransactionDto request, int companyId, int userId, CancellationToken ct = default);
    Task<AccountingTransactionDto> UpdateAsync(int id, int companyId, AccountingTransactionDto request, int userId, CancellationToken ct = default);
    Task DeleteAsync(int id, int companyId, CancellationToken ct = default);
}