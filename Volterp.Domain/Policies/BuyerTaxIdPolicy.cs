using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.Domain.Policies;

// The tax id of the buyer that a document has to name.
//
// The rule is the one of the DGII: a credit fiscal (E31 in electronic, B01
// in paper) always names its buyer, because the buyer deducts the ITBIS. A
// consumption invoice (E32 / B02) names it from RD$250,000 up: below that
// the buyer is the final consumer and the document carries no buyer.
public static class BuyerTaxIdPolicy
{
    // From RD$250,000 the consumption invoice is nominative. It is the same
    // threshold the RFCE uses: the summary exists below it.
    public static decimal NominativeFrom => RfceDocument.MaximumAmount;

    public static bool Requires(EcfDocumentType documentType, decimal total) => documentType switch
    {
        EcfDocumentType.FiscalCredit => true,
        EcfDocumentType.Consumer => total >= NominativeFrom,
        _ => false
    };
}
