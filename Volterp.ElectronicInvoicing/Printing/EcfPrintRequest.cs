using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.ElectronicInvoicing.Printing;

// What the printed representation is built from: the document that was
// signed, the security code of its signature, the URL the QR opens and the
// image of that QR.
public sealed record EcfPrintRequest
{
    /// <summary>The document the representation shows.</summary>
    public required EcfDocument Document { get; init; }

    /// <summary>The additional taxes of the document, written apart.</summary>
    public EcfPrintAdditionalTaxes? AdditionalTaxes { get; init; }

    /// <summary>The pagination declared by the signed document.</summary>
    public EcfPrintPagination? Pagination { get; init; }

    /// <summary>Whether the signed line amounts already include ITBIS.</summary>
    public bool ItemAmountsIncludeItbis { get; init; }

    /// <summary>The security code the taxpayer prints next to the QR.</summary>
    public required string SecurityCode { get; init; }

    /// <summary>The verification URL the QR carries.</summary>
    public required string QrUrl { get; init; }

    /// <summary>The image of the QR, as SVG markup.</summary>
    public required string QrImageSvg { get; init; }
}
