using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Printing;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.Application.DTOs.Printing;

// What the printed representation of an e-CF is built from: the document
// that was signed, the security code of its signature and the URL the QR of
// the representation opens.
public record ElectronicInvoicePrintDto
{
    public Guid ElectronicInvoiceId { get; init; }

    public string ENCF { get; init; } = string.Empty;

    public EcfDocumentType DocumentType { get; init; }

    public string SecurityCode { get; init; } = string.Empty;

    public string QrUrl { get; init; } = string.Empty;

    public required EcfDocument Document { get; init; }

    public EcfPrintAdditionalTaxes? AdditionalTaxes { get; init; }

    public EcfPrintPagination? Pagination { get; init; }

    public bool ItemAmountsIncludeItbis { get; init; }
}
