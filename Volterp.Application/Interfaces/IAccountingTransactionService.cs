using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IAccountingTransactionService
{
    Task<PagedResult<AccountingTransactionDto>> GetAllByCompanyAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, AccountingTransactionDto?>> GetByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<Either<Error, AccountingTransactionDto>> CreateAsync(AccountingTransactionDto request, int companyId, int userId, CancellationToken ct = default);
    Task<Either<Error, AccountingTransactionDto>> UpdateAsync(int id, int companyId, AccountingTransactionDto request, int userId, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeleteAsync(int id, int companyId, CancellationToken ct = default);
}