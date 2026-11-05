using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.Services;

public class AccountingTransactionService(IUnitOfWork unitOfWork) : IAccountingTransactionService
{
    private const string CodeAlreadyInUseMessage = "The code is already in use.";

    private static AccountingTransactionDto MapToDto(AccountingTransaction t) => new AccountingTransactionDto(
        t.Id, t.TransactionType, t.Amount, t.Description, t.ReferenceNumber,
        t.TransactionDate, t.Category, t.Notes, t.Status, t.Code
    );

    public async Task<PagedResult<AccountingTransactionDto>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var transactions = await unitOfWork
            .AccountingTransactions
            .GetAllAsync(pageNumber, pageSize, ct);

        return transactions.Map(MapToDto);
    }

    public async Task<Either<Error, AccountingTransactionDto?>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async transactionId => await unitOfWork.AccountingTransactions.GetByIdAsync(transactionId, ct),
                ex => new Error(ex.Message))
            .Map(transaction => transaction is null ? null : MapToDto(transaction))
            .Run();
    }

    public async Task<Either<Error, AccountingTransactionDto>> CreateAsync(AccountingTransactionDto request, Guid companyId, Guid userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, AccountingTransactionDto>
            .FromRight(request)
            .Ensure(r => !string.IsNullOrWhiteSpace(r.Description), new Error("The description is required."))
            .FlatMap(async req =>
            {
                // The user may bring its own code; otherwise the per-company sequence
                // provides one. A user-provided code must be free inside the company.
                var code = await ResolveCodeForCreateAsync(req.Code, companyId, ct);

                var transaction = new AccountingTransaction
                {
                    CompanyId = companyId,
                    Code = code,
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

    public async Task<Either<Error, AccountingTransactionDto>> UpdateAsync(Guid id, Guid companyId, AccountingTransactionDto request, Guid userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async transactionId => await unitOfWork.AccountingTransactions.GetByIdAsync(transactionId, ct),
                ex => new Error(ex.Message))
            .Ensure(transaction => transaction is not null, new Error("Accounting transaction not found."))
            .FlatMap(async transaction =>
            {
                // An empty code keeps the current one; a new code must be free
                // inside the company (the row being updated is excluded).
                var resolvedCode = await ResolveCodeForUpdateAsync(request.Code, transaction.Code, companyId, id, ct);

                transaction.Apply(t =>
                {
                    t.TransactionType = request.TransactionType;
                    t.Amount = request.Amount;
                    t.Description = request.Description;
                    t.ReferenceNumber = request.ReferenceNumber;
                    t.TransactionDate = request.TransactionDate.AsUtc();
                    t.Category = request.Category;
                    t.Notes = request.Notes;
                    t.Status = request.Status;
                    t.Code = resolvedCode;
                    t.UpdatedAt = DateTime.UtcNow;
                    t.UpdatedBy = userId;
                });
                return transaction;
            }, ex => new Error(ex.Message))
            .FlatMap(async transaction =>
            {
                await unitOfWork.AccountingTransactions.UpdateTransactionAsync(transaction, ct);
                await unitOfWork.CommitAsync(ct);
                return transaction;
            }, ex => new Error(ex.Message))
            .Map(MapToDto)
            .Run();
    }

    public async Task<Either<Error, Unit>> DeleteAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async transactionId => await unitOfWork.AccountingTransactions.GetByIdAsync(transactionId, ct),
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

    /// <summary>
    /// Resolves the code of a new accounting transaction: the user-provided one (trimmed)
    /// when it comes with a value, otherwise the next code of the company sequence.
    /// </summary>
    private async Task<string> ResolveCodeForCreateAsync(string? requestedCode, Guid companyId, CancellationToken ct)
    {
        var trimmed = requestedCode?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return await unitOfWork.Codes.NextAsync(companyId, CodeEntityType.AccountingTransaction, ct);

        if (await unitOfWork.AccountingTransactions.ExistsCodeAsync(companyId, trimmed, null, ct))
            throw new Exception(CodeAlreadyInUseMessage);

        return trimmed;
    }

    /// <summary>
    /// Resolves the code of an existing accounting transaction: the user-provided one
    /// (trimmed) when it comes with a value, otherwise the code it already has.
    /// </summary>
    private async Task<string> ResolveCodeForUpdateAsync(string? requestedCode, string currentCode, Guid companyId, Guid id, CancellationToken ct)
    {
        var trimmed = requestedCode?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return currentCode;

        if (await unitOfWork.AccountingTransactions.ExistsCodeAsync(companyId, trimmed, id, ct))
            throw new Exception(CodeAlreadyInUseMessage);

        return trimmed;
    }
}
