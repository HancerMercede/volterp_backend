namespace Volterp.ElectronicInvoicing;

/// <summary>
/// The tax treatment of a line, as the DGII codes it in the
/// <c>IndicadorFacturacion</c> field of the "Formato Comprobante Fiscal
/// Electrónico (e-CF) v1.0". The persisted value is the DGII code.
/// </summary>
public enum TaxCode
{
    NoFacturable = 0,
    ItbisTasa1 = 1,
    ItbisTasa2 = 2,
    ItbisTasa3 = 3,
    Exento = 4
}
