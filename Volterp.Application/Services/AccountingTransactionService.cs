using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class AccountingTransactionService(IUnitOfWork unitOfWork) : IAccountingTransactionService
{
    private static AccountingTransactionDto MapToDto(AccountingTransaction t) => new AccountingTransactionDto(
        t.Id, t.TransactionType, t.Amount, t.Description, t.ReferenceNumber,
        t.TransactionDate, t.Category, t.Notes, t.Status
    );

    public async Task<PagedResult<AccountingTransactionDto>> GetAllByCompanyAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var transactions = await unitOfWork
            .AccountingTransactions
            .GetAllByCompanyAsync(companyId, pageNumber, pageSize, ct);

        return transactions.Map(MapToDto);
    }

    public async Task<Either<Error, AccountingTransactionDto?>> GetByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async transactionId => await unitOfWork.AccountingTransactions.GetByIdAsync(transactionId, companyId, ct),
                ex => new Error(ex.Message))
            .Map(transaction => transaction is null ? null : MapToDto(transaction))
            .Run();
    }

    public async Task<Either<Error, AccountingTransactionDto>> CreateAsync(AccountingTransactionDto request, int companyId, int userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, AccountingTransactionDto>
            .FromRight(request)
            .Ensure(r => !string.IsNullOrWhiteSpace(r.Description), new Error("The description is required."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async req =>
            {
                var transaction = new AccountingTransaction
                {
                    CompanyId = companyId,
                    TransactionType = req.TransactionType,
                    Amount = req.Amount,
                    Description = req.Description,
                    ReferenceNumber = req.ReferenceNumber,
                    TransactionDate = req.TransactionDate,
                    Category = req.Category,
                    Notes = req.Notes,
                    Status = req.Status,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };
            
                await unitOfWork.AccountingTransactions.AddTransactionAsync(transaction, ct);
                await unitOfWork.CommitAsync(ct);
                return transaction;
            }, ex => new Error(ex.Message))
            .Map(MapToDto)
            .Run();
    }

    public async Task<Either<Error, AccountingTransactionDto>> UpdateAsync(int id, int companyId, AccountingTransactionDto request, int userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async transactionId => await unitOfWork.AccountingTransactions.GetByIdAsync(transactionId, companyId, ct),
                ex => new Error(ex.Message))
            .Ensure(transaction => transaction is not null, new Error("Accounting transaction not found."))
            .Map(transaction =>
            {
                transaction.Apply(t =>
                {
                    t.TransactionType = request.TransactionType;
                    t.Amount = request.Amount;
                    t.Description = request.Description;
                    t.ReferenceNumber = request.ReferenceNumber;
                    t.TransactionDate = request.TransactionDate;
                    t.Category = request.Category;
                    t.Notes = request.Notes;
                    t.Status = request.Status;
                    t.UpdatedAt = DateTime.UtcNow;
                    t.UpdatedBy = userId;
                });
                return transaction;
            })
            .FlatMap(async transaction =>
            {
                await unitOfWork.AccountingTransactions.UpdateTransactionAsync(transaction, ct);
                await unitOfWork.CommitAsync(ct);
                return transaction;
            }, ex => new Error(ex.Message))
            .Map(MapToDto)
            .Run();
    }

    public async Task<Either<Error, Unit>> DeleteAsync(int id, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async transactionId => await unitOfWork.AccountingTransactions.GetByIdAsync(transactionId, companyId, ct),
                ex => new Error(ex.Message))
            .Ensure(transaction => transaction is not null, new Error("Accounting transaction not found."))
            .FlatMap(async transaction =>
            {
                await unitOfWork.AccountingTransactions.DeleteTransactionAsync(transaction.Id, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, ex => new Error(ex.Message))
            .Run();
    }
}