namespace Volterp.ElectronicInvoicing;

/// <summary>
/// The ten e-CF types the DGII defines. The value is the official two-digit
/// type code, which is also the second part of the e-NCF.
/// </summary>
public enum EcfDocumentType
{
    FiscalCredit = 31,
    Consumer = 32,
    DebitNote = 33,
    CreditNote = 34,
    Purchase = 41,
    MinorExpenses = 43,
    SpecialRegimes = 44,
    Governmental = 45,
    Exportation = 46,
    ExternalPayments = 47
}
