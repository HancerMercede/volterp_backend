using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;

namespace Volterp.Application.DTOs.ElectronicInvoiceDtos;

public record ElectronicInvoiceDto : IMapFrom<ElectronicInvoice>
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SaleId { get; set; }
    public string ENCF { get; set; } = string.Empty;
    public EcfDocumentType DocumentType { get; set; }
    public string IssuerTaxId { get; set; } = string.Empty;
    public string? ReceiverTaxId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Itbis { get; set; }
    public decimal Total { get; set; }
    public EcfStatus Status { get; set; }
    public string? TrackId { get; set; }
    public string? SecurityCode { get; set; }
    public string? QrUrl { get; set; }
    public string? VerdictNote { get; set; }
    public DateTime? SignedAt { get; set; }
    public DateTime? SentAt { get; set; }

    public void MapFrom(ElectronicInvoice source)
    {
        Id = source.Id;
        CompanyId = source.CompanyId;
        SaleId = source.SaleId;
        ENCF = source.ENCF;
        DocumentType = source.DocumentType;
        IssuerTaxId = source.IssuerTaxId;
        ReceiverTaxId = source.ReceiverTaxId;
        Subtotal = source.Subtotal;
        Itbis = source.Itbis;
        Total = source.Total;
        Status = source.Status;
        TrackId = source.TrackId;
        SecurityCode = source.SecurityCode;
        QrUrl = source.QrUrl;
        VerdictNote = source.VerdictNote;
        SignedAt = source.SignedAt;
        SentAt = source.SentAt;
    }
}
