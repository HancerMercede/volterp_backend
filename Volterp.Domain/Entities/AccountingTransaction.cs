using Volterp.Domain.Enums;

namespace Volterp.Domain.Entities;

public class AccountingTransaction : AuditEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public TransactionType TransactionType { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public Company Company { get; set; } = null!;
}