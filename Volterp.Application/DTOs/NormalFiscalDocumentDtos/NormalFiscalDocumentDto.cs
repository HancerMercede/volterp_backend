using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.NormalFiscalDocumentDtos;

public record NormalFiscalDocumentDto : IMapFrom<NormalFiscalDocument>
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SaleId { get; set; }
    public string Number { get; set; } = string.Empty;
    public NormalFiscalDocumentType DocumentType { get; set; }
    public string IssuerTaxId { get; set; } = string.Empty;
    public string? ReceiverTaxId { get; set; }
    public string? ReceiverName { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Itbis { get; set; }
    public decimal Total { get; set; }
    public DateTime IssuedAt { get; set; }

    public void MapFrom(NormalFiscalDocument source)
    {
        Id = source.Id;
        CompanyId = source.CompanyId;
        SaleId = source.SaleId;
        Number = source.Number;
        DocumentType = source.DocumentType;
        IssuerTaxId = source.IssuerTaxId;
        ReceiverTaxId = source.ReceiverTaxId;
        ReceiverName = source.ReceiverName;
        Subtotal = source.Subtotal;
        Itbis = source.Itbis;
        Total = source.Total;
        IssuedAt = source.IssuedAt;
    }
}
