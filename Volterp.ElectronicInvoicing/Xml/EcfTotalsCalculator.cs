namespace Volterp.ElectronicInvoicing.Xml;

/// <summary>
/// Turns the lines of a document into the fiscal totals the "Totales" area
/// requires. The amount of a line is the taxable base, before taxes, which is
/// what the format calls "MontoItem".
/// </summary>
public static class EcfTotalsCalculator
{
    /// <summary>
    /// Calculates the totals of a document from its lines.
    /// </summary>
    /// <param name="items">The lines of the document.</param>
    public static EcfTotals Calculate(IEnumerable<EcfItem> items) => Calculate(items, []);

    /// <summary>
    /// Calculates the totals of a document from its lines and the discounts and
    /// surcharges of the whole document: they change the base the ITBIS is
    /// charged on, so they are applied to the line they name before the totals
    /// are added up.
    /// </summary>
    /// <param name="items">The lines of the document.</param>
    /// <param name="adjustments">The discounts and surcharges of the document.</param>
    public static EcfTotals Calculate(IEnumerable<EcfItem> items, IEnumerable<EcfAdjustment> adjustments)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(adjustments);

        var netByLine = NetByLine(adjustments);
        var buckets = new Dictionary<EcfTaxBucket, (decimal Amount, decimal Itbis)>();

        foreach (var line in items)
        {
            var rate = EcfTaxRates.For(line.TaxTreatment);
            var amount = decimal.Round(line.Amount + netByLine.GetValueOrDefault(line.LineNumber), 2);
            var tax = decimal.Round(amount * rate.Fraction, 2);

            var current = buckets.GetValueOrDefault(rate.Bucket);
            buckets[rate.Bucket] = (current.Amount + amount, current.Itbis + tax);
        }

        var taxableTotal = Sum(buckets, found => found.Amount, EcfTaxBucket.Rate1, EcfTaxBucket.Rate2, EcfTaxBucket.Rate3);
        var exemptTotal = Amount(buckets, EcfTaxBucket.Exempt);
        var itbisTotal = Sum(buckets, found => found.Itbis, EcfTaxBucket.Rate1, EcfTaxBucket.Rate2, EcfTaxBucket.Rate3);

        return new EcfTotals
        {
            TaxableTotal = taxableTotal,
            TaxableAtRate1 = Amount(buckets, EcfTaxBucket.Rate1),
            TaxableAtRate2 = Amount(buckets, EcfTaxBucket.Rate2),
            TaxableAtRate3 = Amount(buckets, EcfTaxBucket.Rate3),
            ExemptTotal = exemptTotal,
            NonBillableTotal = Amount(buckets, EcfTaxBucket.NonBillable),
            ItbisRate1 = Percent(buckets, EcfTaxBucket.Rate1),
            ItbisRate2 = Percent(buckets, EcfTaxBucket.Rate2),
            ItbisRate3 = Percent(buckets, EcfTaxBucket.Rate3),
            ItbisAtRate1 = Tax(buckets, EcfTaxBucket.Rate1),
            ItbisAtRate2 = Tax(buckets, EcfTaxBucket.Rate2),
            ItbisAtRate3 = Tax(buckets, EcfTaxBucket.Rate3),
            ItbisTotal = itbisTotal,
            Total = taxableTotal + (exemptTotal ?? 0m) + itbisTotal
        };
    }

    // The discount takes the amount away and the surcharge adds it, and both
    // land on the line they name.
    private static Dictionary<int, decimal> NetByLine(IEnumerable<EcfAdjustment> adjustments)
    {
        var net = new Dictionary<int, decimal>();

        foreach (var adjustment in adjustments)
        {
            var amount = adjustment.Amount ?? 0m;

            if (adjustment.Kind == EcfAdjustmentKind.Discount)
                amount = -amount;

            net[adjustment.LineNumber] = net.GetValueOrDefault(adjustment.LineNumber) + amount;
        }

        return net;
    }

    private static decimal? Amount(Dictionary<EcfTaxBucket, (decimal Amount, decimal Itbis)> buckets, EcfTaxBucket bucket)
        => buckets.TryGetValue(bucket, out var found) ? found.Amount : null;

    private static decimal? Tax(Dictionary<EcfTaxBucket, (decimal Amount, decimal Itbis)> buckets, EcfTaxBucket bucket)
        => buckets.TryGetValue(bucket, out var found) ? found.Itbis : null;

    private static int? Percent(Dictionary<EcfTaxBucket, (decimal Amount, decimal Itbis)> buckets, EcfTaxBucket bucket)
        => buckets.ContainsKey(bucket) ? EcfTaxRates.For(bucket)?.Percent : null;

    private static decimal Sum(
        Dictionary<EcfTaxBucket, (decimal Amount, decimal Itbis)> buckets,
        Func<(decimal Amount, decimal Itbis), decimal> selector,
        params EcfTaxBucket[] wanted)
    {
        var total = 0m;

        foreach (var bucket in wanted)
        {
            if (buckets.TryGetValue(bucket, out var found))
                total += selector(found);
        }

        return total;
    }
}
