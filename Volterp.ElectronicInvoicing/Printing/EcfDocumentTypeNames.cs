namespace Volterp.ElectronicInvoicing.Printing;

// The official name of each type of e-CF: the one the printed representation
// carries as the type of the document.
//
// The debit note is the 33 and the credit note is the 34. A layout that
// swaps them prints a document of the wrong type, and the mistake is paid
// at the counter.
public static class EcfDocumentTypeNames
{
    public static string Of(EcfDocumentType documentType) => documentType switch
    {
        EcfDocumentType.FiscalCredit => "Factura de Crédito Fiscal Electrónica",
        EcfDocumentType.Consumer => "Factura de Consumo Electrónica",
        EcfDocumentType.DebitNote => "Nota de Débito Electrónica",
        EcfDocumentType.CreditNote => "Nota de Crédito Electrónica",
        EcfDocumentType.Purchase => "Comprobante Electrónico de Compras",
        EcfDocumentType.MinorExpenses => "Comprobante Electrónico para Gastos Menores",
        EcfDocumentType.SpecialRegimes => "Comprobante Electrónico para Regímenes Especiales",
        EcfDocumentType.Governmental => "Comprobante Electrónico Gubernamental",
        EcfDocumentType.Exportation => "Comprobante Electrónico para Exportaciones",
        EcfDocumentType.ExternalPayments => "Comprobante Electrónico para Pagos al Exterior",
        _ => throw new ArgumentOutOfRangeException(nameof(documentType), documentType, "Unknown type of e-CF.")
    };
}
