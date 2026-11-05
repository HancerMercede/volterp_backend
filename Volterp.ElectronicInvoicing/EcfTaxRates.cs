namespace Volterp.ElectronicInvoicing;

/// <summary>
/// The bucket of the fiscal totals a tax code belongs to. Every bucket has
/// its own field in the "Totales" area of the e-CF format.
/// </summary>
public enum EcfTaxBucket
{
    /// <summary>Lines that cannot be invoiced, reported in "MontoNoFacturable".</summary>
    NonBillable = 0,

    /// <summary>Lines taxed at rate one, reported in "MontoGravadoI1".</summary>
    Rate1 = 1,

    /// <summary>Lines taxed at rate two, reported in "MontoGravadoI2".</summary>
    Rate2 = 2,

    /// <summary>Lines taxed at rate three, reported in "MontoGravadoI3".</summary>
    Rate3 = 3,

    /// <summary>Exempt lines, reported in "MontoExento".</summary>
    Exempt = 4
}

/// <summary>
/// How the DGII treats one tax code: the bucket of the fiscal totals it
/// belongs to and the ITBIS it pays.
/// </summary>
public sealed record EcfTaxRate
{
    /// <summary>The tax code this rate describes.</summary>
    public required TaxCode Code { get; init; }

    /// <summary>The bucket of the fiscal totals the code belongs to.</summary>
    public required EcfTaxBucket Bucket { get; init; }

    /// <summary>The ITBIS the code pays, as a fraction of the taxable amount.</summary>
    public required decimal Fraction { get; init; }

    /// <summary>
    /// The ITBIS the code pays, as the percentage the DGII codes in the
    /// "ITBIS1", "ITBIS2" and "ITBIS3" fields. Null when the code pays no
    /// ITBIS at all.
    /// </summary>
    public int? Percent { get; init; }
}

/// <summary>
/// The ITBIS rates of the e-CF format. This is the only place where a tax
/// code becomes a rate, so the totals of a document and the totals of a sale
/// can never drift apart.
/// </summary>
public static class EcfTaxRates
{
    /// <summary>The rate of a tax code.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The code is not one of the official ones.</exception>
    public static EcfTaxRate For(TaxCode code) => code switch
    {
        TaxCode.NoFacturable => new() { Code = code, Bucket = EcfTaxBucket.NonBillable, Fraction = 0m, Percent = null },
        TaxCode.ItbisTasa1 => new() { Code = code, Bucket = EcfTaxBucket.Rate1, Fraction = 0.18m, Percent = 18 },
        TaxCode.ItbisTasa2 => new() { Code = code, Bucket = EcfTaxBucket.Rate2, Fraction = 0.16m, Percent = 16 },
        TaxCode.ItbisTasa3 => new() { Code = code, Bucket = EcfTaxBucket.Rate3, Fraction = 0m, Percent = 0 },
        TaxCode.Exento => new() { Code = code, Bucket = EcfTaxBucket.Exempt, Fraction = 0m, Percent = null },
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown tax code.")
    };

    /// <summary>The rate of a bucket, or null when the bucket pays no ITBIS.</summary>
    public static EcfTaxRate? For(EcfTaxBucket bucket) => bucket switch
    {
        EcfTaxBucket.Rate1 => For(TaxCode.ItbisTasa1),
        EcfTaxBucket.Rate2 => For(TaxCode.ItbisTasa2),
        EcfTaxBucket.Rate3 => For(TaxCode.ItbisTasa3),
        _ => null
    };

    /// <summary>The fraction of ITBIS a tax code pays.</summary>
    public static decimal FractionOf(TaxCode code) => For(code).Fraction;
}
