using System.Text.Json.Serialization;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;

namespace Volterp.Application.DTOs.ReceivedElectronicInvoiceDtos;

public record ReceivedElectronicInvoiceDto : IMapFrom<ReceivedElectronicInvoice>
{
    public Guid Id { get; set; }

    [JsonIgnore]
    public Guid CompanyId { get; set; }

    public string IssuerTaxId { get; set; } = string.Empty;
    public string ReceiverTaxId { get; set; } = string.Empty;
    public string ENCF { get; set; } = string.Empty;
    public EcfDocumentType DocumentType { get; set; }
    public DateTime SignedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public ReceivedElectronicInvoiceApprovalStatus ApprovalStatus { get; set; }
    public string? ApprovalDecidedBy { get; set; }
    public DateTime? ApprovalDecidedAt { get; set; }
    public string? ApprovalReason { get; set; }

    // The flag says whether the document was already on record when the
    // operation arrived. It comes from the operation, not from the entity,
    // so the service sets it when it maps the entity.
    public bool AlreadyReceived { get; set; }

    public void MapFrom(ReceivedElectronicInvoice source)
    {
        Id = source.Id;
        CompanyId = source.CompanyId;
        IssuerTaxId = source.IssuerTaxId;
        ReceiverTaxId = source.ReceiverTaxId;
        ENCF = source.ENCF;
        DocumentType = source.DocumentType;
        SignedAt = source.SignedAt;
        ReceivedAt = source.ReceivedAt;
        ApprovalStatus = source.ApprovalStatus;
        ApprovalDecidedBy = source.ApprovalDecidedBy;
        ApprovalDecidedAt = source.ApprovalDecidedAt;
        ApprovalReason = source.ApprovalReason;
    }
}
