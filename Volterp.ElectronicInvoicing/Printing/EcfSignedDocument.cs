using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.ElectronicInvoicing.Printing;

// What a signed e-CF says: the document itself and the additional taxes that
// the printed representation writes apart.
public sealed record EcfSignedDocument
{
    /// <summary>The document that was signed.</summary>
    public required EcfDocument Document { get; init; }

    /// <summary>The additional taxes of the document.</summary>
    public required EcfPrintAdditionalTaxes AdditionalTaxes { get; init; }

    /// <summary>The pagination declared by the signed document.</summary>
    public EcfPrintPagination? Pagination { get; init; }

    /// <summary>Whether the signed line amounts already include ITBIS.</summary>
    public bool ItemAmountsIncludeItbis { get; init; }
}
