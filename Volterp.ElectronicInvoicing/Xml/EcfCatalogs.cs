using System.Globalization;

namespace Volterp.ElectronicInvoicing.Xml;

/// <summary>
/// The type of income the sale represents, as the DGII codes it in the
/// <c>TipoIngresos</c> field. The emitted code is two digits.
/// </summary>
public enum EcfTypeIncome
{
    Operations = 1,
    Financial = 2,
    Extraordinary = 3,
    Leases = 4,
    DepreciableAssetSale = 5,
    Other = 6
}

/// <summary>
/// The codes the DGII gives to the kinds of income, as the schema of an e-CF
/// enumerates them.
/// </summary>
public static class EcfTypeIncomes
{
    /// <summary>
    /// The kind of income a code of the schema names, or null when the code is
    /// not one of them.
    /// </summary>
    public static EcfTypeIncome? FromCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (!int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            return null;

        return Enum.IsDefined((EcfTypeIncome)value) ? (EcfTypeIncome)value : null;
    }

    /// <summary>The code the schema uses for a kind of income.</summary>
    public static string Code(EcfTypeIncome income) =>
        ((int)income).ToString("D2", CultureInfo.InvariantCulture);
}

/// <summary>
/// How the buyer pays, as the DGII codes it in the <c>TipoPago</c> field.
/// </summary>
public enum EcfPaymentType
{
    Cash = 1,
    Credit = 2,
    Free = 3
}

/// <summary>
/// The method of payment, as the DGII codes it in the <c>FormaPago</c> field.
/// </summary>
public enum EcfPaymentMethod
{
    Cash = 1,
    CheckTransferDeposit = 2,
    DebitCreditCard = 3,
    SaleOnCredit = 4,
    GiftCertificates = 5,
    Barter = 6,
    CreditNote = 7,
    Other = 8
}

/// <summary>
/// The kind of account a payment comes from, as the DGII codes it in the
/// <c>TipoCuentaPago</c> field.
/// </summary>
public enum EcfAccountType
{
    Current = 1,
    Savings = 2,
    Other = 3
}

/// <summary>
/// Whether a line sells goods or services, as the DGII codes it in the
/// <c>IndicadorBienoServicio</c> field.
/// </summary>
public enum EcfItemKind
{
    Goods = 1,
    Services = 2
}

/// <summary>
/// Whether a discount or a surcharge, as the DGII codes it in the
/// <c>TipoAjuste</c> field.
/// </summary>
public enum EcfAdjustmentKind
{
    Discount = 'D',
    Surcharge = 'R'
}

/// <summary>
/// Whether an adjustment is expressed as an amount or a percentage, as the
/// DGII codes it in the <c>TipoValor</c> field.
/// </summary>
public enum EcfAdjustmentValueKind
{
    Amount = '$',
    Percentage = '%'
}

/// <summary>
/// Why a credit or debit note modifies another document, as the DGII codes it
/// in the <c>CodigoModificacion</c> field.
/// </summary>
public enum EcfModificationCode
{
    AnnulsModifiedNcf = 1,
    CorrectsText = 2,
    CorrectsAmounts = 3,
    ReplacesContingencyNcf = 4,
    ReferencesConsumerInvoice = 5
}
