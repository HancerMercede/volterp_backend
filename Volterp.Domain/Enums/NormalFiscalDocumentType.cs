using Volterp.ElectronicInvoicing;

namespace Volterp.Domain.Enums;

// The normal fiscal documents the DGII authorizes on paper (Serie B). The
// value is the official two-digit type code that opens the number of the
// document.
public enum NormalFiscalDocumentType
{
    FiscalCredit = 1,
    Consumer = 2,
    DebitNote = 3,
    CreditNote = 4
}

// The normal counterpart of an electronic type. The electronic code is not the
// normal one: the credit fiscal invoice is 31 electronically and B01 on
// paper. The types the taxpayer does not issue on paper have none.
public static class NormalFiscalDocumentTypes
{
    public static NormalFiscalDocumentType? For(EcfDocumentType documentType) => documentType switch
    {
        EcfDocumentType.FiscalCredit => NormalFiscalDocumentType.FiscalCredit,
        EcfDocumentType.Consumer => NormalFiscalDocumentType.Consumer,
        EcfDocumentType.DebitNote => NormalFiscalDocumentType.DebitNote,
        EcfDocumentType.CreditNote => NormalFiscalDocumentType.CreditNote,
        _ => null
    };

    // The DGII gives no expiry date to the range of the consumption invoice
    // and to the range of the credit note: they never expire.
    public static bool RequiresExpiry(NormalFiscalDocumentType documentType)
        => documentType is not (NormalFiscalDocumentType.Consumer or NormalFiscalDocumentType.CreditNote);
}
