namespace Volterp.ElectronicInvoicing.Xml;

// The summary of a consumption invoice below RD$250,000 ("Formato de Resumen
// Factura de Consumo Electrónica <DOP250,000"): the document the taxpayer
// sends to the DGII in place of the invoice itself.
public sealed record RfceDocument
{
    // Above this amount the invoice itself is the one that travels to the
    // DGII and there is no summary.
    public const decimal MaximumAmount = 250000m;

    public required RfceIdentity Identity { get; init; }

    public required RfceIssuer Issuer { get; init; }

    // The buyer of a consumption invoice may not be identified at all.
    public RfceBuyer? Buyer { get; init; }

    public required RfceTotals Totals { get; init; }

    // The code of security of the invoice this summary stands for: the first
    // six characters of the SignatureValue of the invoice it summarizes. It
    // cannot be the one of the summary, because the tag that carries it is
    // part of what the signature of the summary covers.
    public required string SecurityCode { get; init; }
}

public sealed record RfceIdentity
{
    public required string ENCF { get; init; }

    public EcfTypeIncome? TypeIncome { get; init; }

    public EcfPaymentType? PaymentType { get; init; }

    public IReadOnlyList<RfcePayment> Payments { get; init; } = [];
}

public sealed record RfcePayment
{
    public required EcfPaymentMethod Method { get; init; }

    public required decimal Amount { get; init; }
}

public sealed record RfceIssuer
{
    public required string TaxId { get; init; }

    public required string BusinessName { get; init; }

    public required DateTime IssuedAt { get; init; }
}

public sealed record RfceBuyer
{
    public string? TaxId { get; init; }

    // The buyer may be a foreigner without an RNC: the foreign id takes its
    // place.
    public string? ForeignId { get; init; }

    public string? BusinessName { get; init; }
}

public sealed record RfceTotals
{
    public required decimal TaxableTotal { get; init; }

    public decimal? TaxableAtRate1 { get; init; }

    public decimal? TaxableAtRate2 { get; init; }

    public decimal? TaxableAtRate3 { get; init; }

    public decimal? ExemptTotal { get; init; }

    public decimal? ItbisTotal { get; init; }

    public decimal? ItbisAtRate1 { get; init; }

    public decimal? ItbisAtRate2 { get; init; }

    public decimal? ItbisAtRate3 { get; init; }

    public decimal? AdditionalTaxTotal { get; init; }

    public IReadOnlyList<RfceAdditionalTax> AdditionalTaxes { get; init; } = [];

    public required decimal Total { get; init; }

    public decimal? NonBillableTotal { get; init; }

    public decimal? PeriodTotal { get; init; }
}

public sealed record RfceAdditionalTax
{
    // The code of the additional tax, as the "Codificación Tipos de Impuestos
    // Adicionales" of the e-CF format lists it.
    public required int Code { get; init; }

    public decimal? SpecificConsumptionAmount { get; init; }

    public decimal? AdValoremAmount { get; init; }

    public decimal? OtherAmount { get; init; }
}
