using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.ElectronicInvoicing.Printing;

// The layout of the printed representation comes out of the buyer: a buyer
// with a tax id gets the full layout, and a buyer without one gets the
// simplified layout of the final consumer.
public enum EcfPrintTemplate
{
    RegisteredBuyer = 1,
    FinalConsumer = 2
}

public static class EcfPrintTemplates
{
    // The template of a document: the buyer with a tax id is the one the
    // full layout describes, and the buyer without one is the final consumer
    // of the simplified layout. It comes out of the document and not out of
    // the type of the document: a consumption invoice can be sold to a
    // company and a credit invoice to a final consumer.
    public static EcfPrintTemplate For(EcfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var taxId = document.Buyer?.TaxId;

        return string.IsNullOrWhiteSpace(taxId)
            ? EcfPrintTemplate.FinalConsumer
            : EcfPrintTemplate.RegisteredBuyer;
    }

    // The name the layout carries in the representation.
    public static string SlugOf(EcfPrintTemplate template) => template switch
    {
        EcfPrintTemplate.RegisteredBuyer => "registered-buyer",
        EcfPrintTemplate.FinalConsumer => "final-consumer",
        _ => throw new ArgumentOutOfRangeException(nameof(template), template, "Unknown template.")
    };
}
