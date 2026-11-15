using Volterp.Domain.Abstractions;
using Volterp.ElectronicInvoicing;

namespace Volterp.Domain.Entities;

public class ReceivedElectronicInvoice : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }

    public string IssuerTaxId { get; set; } = string.Empty;
    public string ReceiverTaxId { get; set; } = string.Empty;
    public string ENCF { get; set; } = string.Empty;
    public EcfDocumentType DocumentType { get; set; }
    public string SignedXml { get; set; } = string.Empty;
    public DateTime SignedAt { get; set; }
    public DateTime ReceivedAt { get; set; }

    public ReceivedElectronicInvoiceApprovalStatus ApprovalStatus { get; set; }
        = ReceivedElectronicInvoiceApprovalStatus.Received;
    public string? ApprovalDecidedBy { get; set; }
    public DateTime? ApprovalDecidedAt { get; set; }
    public string? ApprovalReason { get; set; }

    public Company Company { get; set; } = null!;

    public void ApproveCommercial(string decidedBy, DateTime decidedAt, string? reason)
    {
        EnsurePendingDecision();
        EnsureDecisionData(decidedBy);

        ApprovalStatus = ReceivedElectronicInvoiceApprovalStatus.Approved;
        ApprovalDecidedBy = decidedBy;
        ApprovalDecidedAt = decidedAt;
        ApprovalReason = string.IsNullOrWhiteSpace(reason) ? null : reason;
    }

    public void RejectCommercial(string decidedBy, DateTime decidedAt, string reason)
    {
        EnsurePendingDecision();
        EnsureDecisionData(decidedBy);

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("The rejection reason cannot be empty.", nameof(reason));

        ApprovalStatus = ReceivedElectronicInvoiceApprovalStatus.Rejected;
        ApprovalDecidedBy = decidedBy;
        ApprovalDecidedAt = decidedAt;
        ApprovalReason = reason;
    }

    private void EnsurePendingDecision()
    {
        if (ApprovalStatus != ReceivedElectronicInvoiceApprovalStatus.Received)
            throw new InvalidOperationException("The commercial approval was already recorded.");
    }

    private static void EnsureDecisionData(string decidedBy)
    {
        if (string.IsNullOrWhiteSpace(decidedBy))
            throw new ArgumentException("The decision maker cannot be empty.", nameof(decidedBy));
    }
}

public enum ReceivedElectronicInvoiceApprovalStatus
{
    Received = 1,
    Approved = 2,
    Rejected = 3
}
