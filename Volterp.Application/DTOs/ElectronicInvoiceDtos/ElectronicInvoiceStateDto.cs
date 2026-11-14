using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;

namespace Volterp.Application.DTOs.ElectronicInvoiceDtos;

// The fiscal state of a document that is still open: what the tenant has to
// act on.
public record ElectronicInvoiceStateDto : IMapFrom<ElectronicInvoice>
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public string ENCF { get; set; } = string.Empty;
    public EcfDocumentType DocumentType { get; set; }

    // The fiscal state is not a field of the document: it is the
    // classification of its status and its review flag. The status travels
    // with it to say how far the document got with the DGII.
    public EcfFiscalState FiscalState { get; set; }
    public EcfStatus Status { get; set; }

    public string? VerdictNote { get; set; }
    public bool RequiresReview { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? NextCheckAt { get; set; }
    public int StatusCheckCount { get; set; }

    public void MapFrom(ElectronicInvoice source)
    {
        Id = source.Id;
        SaleId = source.SaleId;
        ENCF = source.ENCF;
        DocumentType = source.DocumentType;
        Status = source.Status;
        VerdictNote = source.VerdictNote;
        RequiresReview = source.RequiresReview;
        SentAt = source.SentAt;
        NextCheckAt = source.NextCheckAt;
        StatusCheckCount = source.StatusCheckCount;
    }
}
