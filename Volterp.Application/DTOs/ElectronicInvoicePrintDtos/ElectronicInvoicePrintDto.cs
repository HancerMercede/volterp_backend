using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Printing;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.Application.DTOs.ElectronicInvoicePrintDtos;

// What the printed representation of an e-CF is built from: the document
// that was signed, the security code of its signature and the URL the QR of
// the representation opens.
public record ElectronicInvoicePrintDto : IMapFrom<ElectronicInvoice>
{
    public Guid ElectronicInvoiceId { get; set; }

    public string ENCF { get; set; } = string.Empty;

    public EcfDocumentType DocumentType { get; set; }

    public string SecurityCode { get; set; } = string.Empty;

    public string QrUrl { get; set; } = string.Empty;

    public EcfDocument Document { get; set; } = null!;

    public EcfPrintAdditionalTaxes? AdditionalTaxes { get; set; }

    public EcfPrintPagination? Pagination { get; set; }

    public bool ItemAmountsIncludeItbis { get; set; }

    public void MapFrom(ElectronicInvoice source)
    {
        // The representation is built from what was signed: the amounts, the
        // names and the address are the ones the DGII received.
        var signed = EcfSignedDocumentReader.Read(source.SignedXml);

        ElectronicInvoiceId = source.Id;
        ENCF = source.ENCF;
        DocumentType = source.DocumentType;
        SecurityCode = EcfSecurityCode.From(source.SignedXml);
        QrUrl = source.QrUrl;
        Document = signed.Document;
        AdditionalTaxes = signed.AdditionalTaxes;
        Pagination = signed.Pagination;
        ItemAmountsIncludeItbis = signed.ItemAmountsIncludeItbis;
    }
}
