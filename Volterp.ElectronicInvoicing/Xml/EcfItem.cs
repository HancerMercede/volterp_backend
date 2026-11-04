namespace Volterp.ElectronicInvoicing.Xml;

/// <summary>
/// A line of the document, as the <c>Item</c> element of the
/// "Detalle de Bienes o Servicios" area.
/// </summary>
public sealed record EcfItem
{
    /// <summary>The position of the line in the document.</summary>
    public required int LineNumber { get; init; }

    /// <summary>The tax treatment of the line.</summary>
    public required TaxCode TaxTreatment { get; init; }

    /// <summary>The name of the good or service.</summary>
    public required string Name { get; init; }

    /// <summary>Whether the line sells goods or services.</summary>
    public required EcfItemKind Kind { get; init; }

    /// <summary>A longer description of the good or service.</summary>
    public string? Description { get; init; }

    /// <summary>How many units were sold.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>The unit of measure, as the DGII codes it.</summary>
    public int? UnitOfMeasure { get; init; }

    /// <summary>The unit price, before taxes.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>The discount applied to the line.</summary>
    public decimal? DiscountAmount { get; init; }

    /// <summary>The surcharge applied to the line.</summary>
    public decimal? SurchargeAmount { get; init; }

    /// <summary>The total of the line, taxes included.</summary>
    public required decimal Amount { get; init; }

    /// <summary>The codes of the good, such as the supplier's own code.</summary>
    public IReadOnlyList<EcfItemCode> Codes { get; init; } = [];

    /// <summary>The taxes withheld on the line.</summary>
    public EcfRetention? Retention { get; init; }
}

/// <summary>
/// A code that identifies a good, as the <c>CodigosItem</c> element.
/// </summary>
public sealed record EcfItemCode
{
    /// <summary>The kind of code, such as the supplier's own code.</summary>
    public required string Type { get; init; }

    /// <summary>The code itself.</summary>
    public required string Value { get; init; }
}

/// <summary>
/// The taxes withheld on a line, as the <c>Retencion</c> element.
/// </summary>
public sealed record EcfRetention
{
    /// <summary>Whether the issuer is the withholding agent or the perceived one.</summary>
    public required bool IsWithholdingAgent { get; init; }

    /// <summary>The ITBIS withheld.</summary>
    public decimal? ItbisWithheld { get; init; }

    /// <summary>The income tax withheld.</summary>
    public decimal? IncomeTaxWithheld { get; init; }
}

/// <summary>
/// One way the document was paid, as the <c>FormaDePago</c> element.
/// </summary>
public sealed record EcfPayment
{
    /// <summary>The method of payment.</summary>
    public required EcfPaymentMethod Method { get; init; }

    /// <summary>How much was paid this way.</summary>
    public required decimal Amount { get; init; }
}

/// <summary>
/// The account a check or transfer comes from.
/// </summary>
public sealed record EcfPaymentAccount
{
    /// <summary>The kind of account.</summary>
    public required EcfAccountType Type { get; init; }

    /// <summary>The number of the account.</summary>
    public required string Number { get; init; }

    /// <summary>The bank the account belongs to.</summary>
    public string? Bank { get; init; }
}

/// <summary>
/// A document-wide discount or surcharge, as the <c>DescuentoORecargo</c>
/// element of the "Descuentos o Recargos" area.
/// </summary>
public sealed record EcfAdjustment
{
    /// <summary>The line of the document the adjustment applies to.</summary>
    public required int LineNumber { get; init; }

    /// <summary>Whether it is a discount or a surcharge.</summary>
    public required EcfAdjustmentKind Kind { get; init; }

    /// <summary>Whether the adjustment follows the 10-07 norm.</summary>
    public bool? FollowsNorm1007 { get; init; }

    /// <summary>What the adjustment is for.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the value is an amount or a percentage.</summary>
    public EcfAdjustmentValueKind? ValueKind { get; init; }

    /// <summary>The percentage, when the value is a percentage.</summary>
    public decimal? Value { get; init; }

    /// <summary>The amount of the adjustment.</summary>
    public decimal? Amount { get; init; }
}

/// <summary>
/// The document a credit or debit note refers to, as the
/// <c>InformacionReferencia</c> area.
/// </summary>
public sealed record EcfReference
{
    /// <summary>The e-NCF of the document being modified.</summary>
    public string? ModifiedENCF { get; init; }

    /// <summary>The tax id of the other contributor.</summary>
    public string? OtherContributorTaxId { get; init; }

    /// <summary>When the modified document was issued.</summary>
    public DateTime? ModifiedAt { get; init; }

    /// <summary>Why the document is being modified.</summary>
    public EcfModificationCode? ModificationCode { get; init; }

    /// <summary>A description of what changed.</summary>
    public string? ModificationReason { get; init; }
}
