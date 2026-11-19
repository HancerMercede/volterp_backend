namespace Volterp.ElectronicInvoicing.Xml;

/// <summary>
/// A fiscal electronic document ready to be written as XML. The model is
/// neutral: it carries no ERP concepts so the engine stays sellable on its
/// own. Every property maps to one element of the official e-CF format.
/// </summary>
public sealed record EcfDocument
{
    /// <summary>The type of document, which selects the schema that applies.</summary>
    public required EcfDocumentType DocumentType { get; init; }

    /// <summary>The document identity and the payment terms.</summary>
    public required EcfIdentity Identity { get; init; }

    /// <summary>Who issues the document.</summary>
    public required EcfIssuer Issuer { get; init; }

    /// <summary>Who receives the document. Optional for the buyer-less types.</summary>
    public EcfBuyer? Buyer { get; init; }

    /// <summary>The fiscal totals of the document.</summary>
    public required EcfTotals Totals { get; init; }

    /// <summary>The lines of the document. At least one, at most one thousand.</summary>
    public required IReadOnlyList<EcfItem> Items { get; init; }

    /// <summary>The document-wide discounts and surcharges.</summary>
    public IReadOnlyList<EcfAdjustment> Adjustments { get; init; } = [];

    /// <summary>The document a credit or debit note refers to.</summary>
    public EcfReference? Reference { get; init; }

    /// <summary>When the document was signed, in the issuer's local time.</summary>
    public required DateTime SignedAt { get; init; }
}

/// <summary>
/// The <c>IdDoc</c> area: which document this is and how it is paid.
/// </summary>
public sealed record EcfIdentity
{
    /// <summary>The e-NCF the DGII assigned to this document.</summary>
    public required string ENCF { get; init; }

    /// <summary>
    /// When the authorized sequence of e-NCF expires. Only the types that
    /// require it use it.
    /// </summary>
    public required DateTime? SequenceExpiresAt { get; init; }

    /// <summary>
    /// Whether the affected document is older than thirty days, as the DGII
    /// codes it in the "IndicadorNotaCredito" field. A credit note over a
    /// document that old cannot reduce the ITBIS.
    /// </summary>
    public bool? CreditNoteBeyondThirtyDays { get; init; }

    /// <summary>
    /// Whether the amounts of the lines include the ITBIS, as the DGII codes
    /// it in the "IndicadorMontoGravado" field: 0 when they do not, 1 when
    /// they do. The schema leaves the field optional but the DGII asks for
    /// it, so every document carries it.
    /// </summary>
    public required bool LineAmountsIncludeItbis { get; init; }

    /// <summary>The kind of income the sale represents.</summary>
    public EcfTypeIncome? TypeIncome { get; init; }

    /// <summary>How the buyer pays.</summary>
    public EcfPaymentType? PaymentType { get; init; }

    /// <summary>When the payment falls due. Only for credit sales.</summary>
    public DateTime? PaymentDueDate { get; init; }

    /// <summary>The agreed payment term, in words: "30 days", "72 hours".</summary>
    public string? PaymentTerm { get; init; }

    /// <summary>How the amount was paid, one entry per method.</summary>
    public IReadOnlyList<EcfPayment> Payments { get; init; } = [];

    /// <summary>The account a check or transfer comes from.</summary>
    public EcfPaymentAccount? PaymentAccount { get; init; }
}

/// <summary>
/// The <c>Emisor</c> area: who issues the document.
/// </summary>
public sealed record EcfIssuer
{
    /// <summary>The tax id of the issuer.</summary>
    public required string TaxId { get; init; }

    /// <summary>The legal name of the issuer.</summary>
    public required string BusinessName { get; init; }

    /// <summary>The trade name, when it differs from the legal name.</summary>
    public string? TradeName { get; init; }

    /// <summary>The branch that issues the document.</summary>
    public string? Branch { get; init; }

    /// <summary>The address of the issuer.</summary>
    public required string Address { get; init; }

    /// <summary>The municipality of the issuer, as the DGII codes it.</summary>
    public string? Municipality { get; init; }

    /// <summary>The province of the issuer, as the DGII codes it.</summary>
    public string? Province { get; init; }

    /// <summary>The phone numbers of the issuer, at most three.</summary>
    public IReadOnlyList<string> PhoneNumbers { get; init; } = [];

    /// <summary>The email of the issuer.</summary>
    public string? Email { get; init; }

    /// <summary>The website of the issuer.</summary>
    public string? Website { get; init; }

    /// <summary>The economic activity of the issuer.</summary>
    public string? EconomicActivity { get; init; }

    /// <summary>The code of the salesperson.</summary>
    public string? SellerCode { get; init; }

    /// <summary>The internal invoice number.</summary>
    public string? InternalInvoiceNumber { get; init; }

    /// <summary>The internal order number.</summary>
    public string? InternalOrderNumber { get; init; }

    /// <summary>The sales zone.</summary>
    public string? SalesZone { get; init; }

    /// <summary>The sales route.</summary>
    public string? SalesRoute { get; init; }

    /// <summary>Anything else the issuer wants to declare.</summary>
    public string? AdditionalInformation { get; init; }

    /// <summary>When the document was issued.</summary>
    public required DateTime IssuedAt { get; init; }
}

/// <summary>
/// The <c>Comprador</c> area: who receives the document.
/// </summary>
public sealed record EcfBuyer
{
    /// <summary>The tax id of the buyer.</summary>
    public string? TaxId { get; init; }

    /// <summary>The legal name of the buyer.</summary>
    public string? BusinessName { get; init; }

    /// <summary>The contact of the buyer.</summary>
    public string? Contact { get; init; }

    /// <summary>The email of the buyer.</summary>
    public string? Email { get; init; }

    /// <summary>The address of the buyer.</summary>
    public string? Address { get; init; }

    /// <summary>The municipality of the buyer, as the DGII codes it.</summary>
    public string? Municipality { get; init; }

    /// <summary>The province of the buyer, as the DGII codes it.</summary>
    public string? Province { get; init; }

    /// <summary>When the goods or services were delivered.</summary>
    public DateTime? DeliveredAt { get; init; }

    /// <summary>Who receives the delivery.</summary>
    public string? DeliveryContact { get; init; }

    /// <summary>Where the delivery goes, when it differs from the buyer's address.</summary>
    public string? DeliveryAddress { get; init; }

    /// <summary>The phone number of the delivery.</summary>
    public string? AdditionalPhone { get; init; }
}

/// <summary>
/// The <c>Totales</c> area: the fiscal amounts of the document.
/// </summary>
public sealed record EcfTotals
{
    /// <summary>The taxable amount, everything that pays ITBIS.</summary>
    public required decimal TaxableTotal { get; init; }

    /// <summary>The taxable amount at ITBIS rate one.</summary>
    public decimal? TaxableAtRate1 { get; init; }

    /// <summary>The taxable amount at ITBIS rate two.</summary>
    public decimal? TaxableAtRate2 { get; init; }

    /// <summary>The taxable amount at ITBIS rate three.</summary>
    public decimal? TaxableAtRate3 { get; init; }

    /// <summary>The exempt amount.</summary>
    public decimal? ExemptTotal { get; init; }

    /// <summary>The ITBIS rate one, as a percentage.</summary>
    public int? ItbisRate1 { get; init; }

    /// <summary>The ITBIS rate two, as a percentage.</summary>
    public int? ItbisRate2 { get; init; }

    /// <summary>The ITBIS rate three, as a percentage.</summary>
    public int? ItbisRate3 { get; init; }

    /// <summary>The total ITBIS of the document.</summary>
    public decimal? ItbisTotal { get; init; }

    /// <summary>The ITBIS of rate one.</summary>
    public decimal? ItbisAtRate1 { get; init; }

    /// <summary>The ITBIS of rate two.</summary>
    public decimal? ItbisAtRate2 { get; init; }

    /// <summary>The ITBIS of rate three.</summary>
    public decimal? ItbisAtRate3 { get; init; }

    /// <summary>The additional tax, such as the legal tip.</summary>
    public decimal? AdditionalTaxTotal { get; init; }

    /// <summary>The total of the document, taxes included.</summary>
    public required decimal Total { get; init; }

    /// <summary>The amount that is not billable.</summary>
    public decimal? NonBillableTotal { get; init; }

    /// <summary>The amount billed in the period.</summary>
    public decimal? PeriodTotal { get; init; }

    /// <summary>The previous balance.</summary>
    public decimal? PreviousBalance { get; init; }

    /// <summary>The payment advance.</summary>
    public decimal? PaymentAdvance { get; init; }

    /// <summary>The amount to pay.</summary>
    public decimal? AmountToPay { get; init; }

    /// <summary>The ITBIS withheld.</summary>
    public decimal? ItbisWithheld { get; init; }

    /// <summary>The income tax withheld.</summary>
    public decimal? IncomeTaxWithheld { get; init; }

    /// <summary>The ITBIS perceived.</summary>
    public decimal? ItbisPerceived { get; init; }

    /// <summary>The income tax perceived.</summary>
    public decimal? IncomeTaxPerceived { get; init; }
}
