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
    EntityStatus Status,
    // Business code (e.g. "Asi-001"). This DTO is both the request and the response:
    // on input it is optional (empty means "assign the generated one"), on output it
    // always carries the code of the transaction. ReferenceNumber stays untouched —
    // it is the user-written business reference, not the code.
    string? Code = null
);
