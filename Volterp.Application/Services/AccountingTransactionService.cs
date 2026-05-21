using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class AccountingTransactionService(IUnitOfWork unitOfWork) : IAccountingTransactionService
{
    public async Task<PagedResult<AccountingTransactionDto>> GetAllByCompanyAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var transactions = await unitOfWork
            .AccountingTransactions
            .GetAllByCompanyAsync(companyId, pageNumber, pageSize, ct);

        return transactions.Map(t => new AccountingTransactionDto(
            t.Id, t.TransactionType, t.Amount, t.Description, t.ReferenceNumber,
            t.TransactionDate, t.Category, t.Notes, t.Status
        ));
    }

    public async Task<AccountingTransactionDto?> GetByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        var transaction = await unitOfWork
            .AccountingTransactions
            .GetByIdAsync(id, companyId, ct);

        if (transaction is null) return null;

        return transaction.Map(t => new AccountingTransactionDto(
            t.Id, t.TransactionType, t.Amount, t.Description, t.ReferenceNumber,
            t.TransactionDate, t.Category, t.Notes, t.Status
        ));
    }

    public async Task<AccountingTransactionDto> CreateAsync(AccountingTransactionDto request, int companyId, int userId, CancellationToken ct = default)
    {
        var transaction = new AccountingTransaction
        {
            CompanyId = companyId,
            TransactionType = request.TransactionType,
            Amount = request.Amount,
            Description = request.Description,
            ReferenceNumber = request.ReferenceNumber,
            TransactionDate = request.TransactionDate,
            Category = request.Category,
            Notes = request.Notes,
            Status = request.Status,
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        await unitOfWork.AccountingTransactions.AddTransactionAsync(transaction, ct);
        await unitOfWork.CommitAsync(ct);

        return transaction.Map(t => new AccountingTransactionDto(
            t.Id, t.TransactionType, t.Amount, t.Description, t.ReferenceNumber,
            t.TransactionDate, t.Category, t.Notes, t.Status
        ));
    }

    public async Task<AccountingTransactionDto> UpdateAsync(int id, int companyId, AccountingTransactionDto request, int userId, CancellationToken ct = default)
    {
        var transaction = await unitOfWork.AccountingTransactions.GetByIdAsync(id, companyId, ct);

        if (transaction is null)
            throw new ArgumentException("Accounting transaction not found");

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
            t.UpdatedAt = DateTime.Now;
            t.UpdatedBy = userId;
        });

        await unitOfWork.AccountingTransactions.UpdateTransactionAsync(transaction, ct);
        await unitOfWork.CommitAsync(ct);

        return transaction.Map(t => new AccountingTransactionDto(
            t.Id, t.TransactionType, t.Amount, t.Description, t.ReferenceNumber,
            t.TransactionDate, t.Category, t.Notes, t.Status
        ));
    }

    public async Task DeleteAsync(int id, int companyId, CancellationToken ct = default)
    {
        var transaction = await unitOfWork.AccountingTransactions.GetByIdAsync(id, companyId, ct);

        if (transaction is null)
            throw new ArgumentException("Accounting transaction not found");

        await unitOfWork.AccountingTransactions.DeleteTransactionAsync(id, ct);
        await unitOfWork.CommitAsync(ct);
    }
}