using Volterp.Domain.Abstractions;
using Volterp.Domain.Enums;

namespace Volterp.Domain.Entities;

public class AccountingTransaction : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public TransactionType TransactionType { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    public Company Company { get; set; } = null!;
}