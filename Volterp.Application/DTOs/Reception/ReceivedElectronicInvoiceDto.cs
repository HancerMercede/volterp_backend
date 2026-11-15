using System.Text.Json.Serialization;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;

namespace Volterp.Application.DTOs.Reception;

public record ReceivedElectronicInvoiceDto
{
    public Guid Id { get; init; }

    [JsonIgnore]
    public Guid CompanyId { get; init; }

    public string IssuerTaxId { get; init; } = string.Empty;
    public string ReceiverTaxId { get; init; } = string.Empty;
    public string ENCF { get; init; } = string.Empty;
    public EcfDocumentType DocumentType { get; init; }
    public DateTime SignedAt { get; init; }
    public DateTime ReceivedAt { get; init; }
    public ReceivedElectronicInvoiceApprovalStatus ApprovalStatus { get; init; }
    public string? ApprovalDecidedBy { get; init; }
    public DateTime? ApprovalDecidedAt { get; init; }
    public string? ApprovalReason { get; init; }
    public bool AlreadyReceived { get; init; }
}
