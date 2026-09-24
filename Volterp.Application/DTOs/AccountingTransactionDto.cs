using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs;

public record AccountingTransactionDto(
    Guid Id,
    TransactionType TransactionType,
    decimal Amount,
    string Description,
    string ReferenceNumber,
    DateTime TransactionDate,
    string Category,
    string? Notes,
    EntityStatus Status
);
