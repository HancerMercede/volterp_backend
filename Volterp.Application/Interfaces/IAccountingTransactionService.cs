using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IAccountingTransactionService
{
    Task<PagedResult<AccountingTransactionDto>> GetAllByCompanyAsync(Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, AccountingTransactionDto?>> GetByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);
    Task<Either<Error, AccountingTransactionDto>> CreateAsync(AccountingTransactionDto request, Guid companyId, Guid userId, CancellationToken ct = default);
    Task<Either<Error, AccountingTransactionDto>> UpdateAsync(Guid id, Guid companyId, AccountingTransactionDto request, Guid userId, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeleteAsync(Guid id, Guid companyId, CancellationToken ct = default);
}
