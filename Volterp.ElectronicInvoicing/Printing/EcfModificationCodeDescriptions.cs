using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.ElectronicInvoicing.Printing;

// What a modification code means in words: the printed representation writes
// the description of the code, not the code ("Informe Técnico e-CF v1.0",
// section 18.2.1).
public static class EcfModificationCodeDescriptions
{
    public static string Of(EcfModificationCode code) => code switch
    {
        EcfModificationCode.AnnulsModifiedNcf => "Anula el NCF modificado",
        EcfModificationCode.CorrectsText => "Corrige Texto del Comprobante Fiscal modificado",
        EcfModificationCode.CorrectsAmounts => "Corrige montos del NCF modificado",
        EcfModificationCode.ReplacesContingencyNcf => "Reemplazo NCF emitido en contingencia",
        EcfModificationCode.ReferencesConsumerInvoice => "Referencia a factura de consumo electrónica",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown modification code.")
    };
}
