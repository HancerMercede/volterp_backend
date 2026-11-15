namespace Volterp.ElectronicInvoicing.Printing;

public sealed record EcfPrintPagination
{
    public int? TotalPages { get; init; }

    public required IReadOnlyList<EcfPrintPage> Pages { get; init; }
}

public sealed record EcfPrintPage
{
    public required int Number { get; init; }

    public required int FirstLineNumber { get; init; }

    public required int LastLineNumber { get; init; }

    public decimal? TaxableSubtotal { get; init; }

    public decimal? ExemptSubtotal { get; init; }

    public decimal? ItbisSubtotal { get; init; }

    public decimal? SelectiveConsumptionTaxSubtotal { get; init; }

    public decimal? OtherAdditionalTaxesSubtotal { get; init; }

    public decimal? AdditionalTaxSubtotal { get; init; }

    public decimal? NonBillableSubtotal { get; init; }

    public decimal? Total { get; init; }
}
