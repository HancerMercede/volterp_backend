namespace Volterp.ElectronicInvoicing.Printing;

// The additional taxes of a document, as the printed representation writes
// them: the selective consumption tax, the contribution to the development of
// the telecommunications and the legal tip.
//
// The e-CF carries them in the table of additional taxes, each one with the
// code of the tax, and the amount of the selective consumption tax in its own
// fields.
public sealed record EcfPrintAdditionalTaxes
{
    /// <summary>The selective consumption tax (ISC).</summary>
    public decimal? SelectiveConsumptionTax { get; init; }

    /// <summary>The contribution to the development of the telecommunications (CDT).</summary>
    public decimal? TelecomDevelopmentTax { get; init; }

    /// <summary>The legal tip (propina legal).</summary>
    public decimal? LegalTip { get; init; }

    /// <summary>The signed aggregate entries and their unambiguous line attribution.</summary>
    public IReadOnlyList<EcfPrintAdditionalTaxEntry> Entries { get; init; } = [];
}

public sealed record EcfPrintAdditionalTaxEntry
{
    public required string Code { get; init; }

    public decimal? SelectiveConsumptionTax { get; init; }

    public decimal? AdValoremSelectiveConsumptionTax { get; init; }

    public decimal? OtherTax { get; init; }

    public int? LineNumber { get; init; }
}
