using System.Globalization;
using Volterp.ElectronicInvoicing.Schemas;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.ElectronicInvoicing.Validation;

/// <summary>
/// Checks a document against the rules of its type and against its own
/// arithmetic: the two layers the official schema cannot express.
///
/// The rules of a type come from its profile, which is read from the official
/// schema of the type, so the validator and the writer read the same source
/// and cannot drift apart. The arithmetic is checked against the same totals
/// calculator the document is built with, so a document the calculator
/// produces is always a document that adds up. A document has to be built
/// with the overload that takes its adjustments, which is the same while
/// no document carries adjustments.
/// </summary>
public static class EcfDocumentValidator
{
    /// <summary>
    /// The difference an amount may have with the sum of the lines: one cent.
    /// The format writes the amounts with two decimals and the totals are
    /// rounded to the cent, so the only difference a sound document shows is
    /// the last cent. Two cents is an arithmetic mistake, not rounding.
    /// </summary>
    public const decimal AmountTolerance = 0.01m;

    private const string PaymentTypePath = "Encabezado/IdDoc/TipoPago";
    private const string PaymentDueDatePath = "Encabezado/IdDoc/FechaLimitePago";
    private const string PaymentTermPath = "Encabezado/IdDoc/TerminoPago";
    private const string ItemKindPath = "DetallesItems/Item/IndicadorBienoServicio";

    /// <summary>
    /// Checks a document against the rules of its type and against its own
    /// arithmetic.
    /// </summary>
    /// <param name="document">The document to check.</param>
    public static IReadOnlyList<EcfValidationViolation> Validate(EcfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var profile = EcfTypeProfiles.For(document.DocumentType);
        var schema = EcfSchemas.For(document.DocumentType);

        var violations = new List<EcfValidationViolation>();

        CheckTheRulesOfTheType(document, profile, schema, violations);
        CheckTheArithmetic(document, violations);

        return violations;
    }

    private static void CheckTheRulesOfTheType(
        EcfDocument document,
        EcfTypeProfile profile,
        EcfSchema schema,
        List<EcfValidationViolation> violations)
    {
        var type = (int)profile.DocumentType;
        var buyer = document.Buyer;

        if (profile.RequiresBuyer && buyer is null)
            violations.Add(new("buyer", $"<Comprador> is required for the e-CF {type}."));

        if (buyer is not null)
        {
            if (profile.RequiresBuyerTaxId && string.IsNullOrWhiteSpace(buyer.TaxId))
                violations.Add(new("buyer.tax-id", $"<RNCComprador> is required for the e-CF {type}."));

            if (profile.RequiresBuyerName && string.IsNullOrWhiteSpace(buyer.BusinessName))
                violations.Add(new("buyer.name", $"<RazonSocialComprador> is required for the e-CF {type}."));
        }

        if (profile.RequiresSequenceExpiry && document.Identity.SequenceExpiresAt is null)
            violations.Add(new("sequence-expiry", $"<FechaVencimientoSecuencia> is required for the e-CF {type}."));

        CheckTheIncome(document, profile, violations);
        CheckThePayment(document, profile, schema, violations);
        CheckTheKindsOfTheLines(document, schema, violations);
    }

    private static void CheckTheIncome(
        EcfDocument document,
        EcfTypeProfile profile,
        List<EcfValidationViolation> violations)
    {
        var income = document.Identity.TypeIncome;

        if (income is null)
            return;

        var type = (int)profile.DocumentType;

        if (profile.AllowedTypeIncomes.Count == 0)
        {
            violations.Add(new("type-income", $"<TipoIngresos> is not a field the e-CF {type} accepts."));
            return;
        }

        if (!profile.AllowedTypeIncomes.Contains(income.Value))
        {
            var code = EcfTypeIncomes.Code(income.Value);
            violations.Add(new("type-income", $"<TipoIngresos> {code} is not an income code the e-CF {type} accepts."));
        }
    }

    private static void CheckThePayment(
        EcfDocument document,
        EcfTypeProfile profile,
        EcfSchema schema,
        List<EcfValidationViolation> violations)
    {
        var paymentType = document.Identity.PaymentType;

        if (paymentType is not null)
        {
            var allowed = schema.AllowedValues(PaymentTypePath);
            var code = ((int)paymentType.Value).ToString(CultureInfo.InvariantCulture);

            if (allowed.Count > 0 && !allowed.Contains(code))
            {
                var type = (int)profile.DocumentType;
                violations.Add(new("payment-type", $"<TipoPago> {code} is not a payment the e-CF {type} accepts."));
            }
        }

        // A sale on credit says when it falls due and in what term. The schema
        // cannot demand it because there both fields are optional.
        if (paymentType != EcfPaymentType.Credit)
            return;

        // Some types do not carry those fields at all: the 43 does not, so
        // there is nothing to demand of them.
        if (!schema.Allows(PaymentDueDatePath) && !schema.Allows(PaymentTermPath))
            return;

        var identity = document.Identity;

        if (identity.PaymentDueDate is null || string.IsNullOrWhiteSpace(identity.PaymentTerm))
        {
            var type = (int)profile.DocumentType;
            violations.Add(new("payment-terms", $"The e-CF {type} is paid on credit and needs <FechaLimitePago> and <TerminoPago>."));
        }
    }

    private static void CheckTheKindsOfTheLines(
        EcfDocument document,
        EcfSchema schema,
        List<EcfValidationViolation> violations)
    {
        var allowed = schema.AllowedValues(ItemKindPath);

        if (allowed.Count == 0)
            return;

        foreach (var item in document.Items)
        {
            var code = ((int)item.Kind).ToString(CultureInfo.InvariantCulture);

            if (!allowed.Contains(code))
            {
                var type = (int)document.DocumentType;
                violations.Add(new("item-kind", $"<IndicadorBienoServicio> {code} is not a kind the e-CF {type} accepts."));
            }
        }
    }

    private static void CheckTheArithmetic(EcfDocument document, List<EcfValidationViolation> violations)
    {
        var totals = document.Totals;
        var expected = EcfTotalsCalculator.Calculate(document.Items, document.Adjustments);

        Compare(totals.TaxableTotal, expected.TaxableTotal, "MontoGravadoTotal", violations);
        Compare(totals.TaxableAtRate1, expected.TaxableAtRate1, "MontoGravadoI1", violations);
        Compare(totals.TaxableAtRate2, expected.TaxableAtRate2, "MontoGravadoI2", violations);
        Compare(totals.TaxableAtRate3, expected.TaxableAtRate3, "MontoGravadoI3", violations);
        Compare(totals.ExemptTotal, expected.ExemptTotal, "MontoExento", violations);
        Compare(totals.NonBillableTotal, expected.NonBillableTotal, "MontoNoFacturable", violations);
        Compare(totals.ItbisAtRate1, expected.ItbisAtRate1, "TotalITBIS1", violations);
        Compare(totals.ItbisAtRate2, expected.ItbisAtRate2, "TotalITBIS2", violations);
        Compare(totals.ItbisAtRate3, expected.ItbisAtRate3, "TotalITBIS3", violations);
        Compare(totals.ItbisTotal, expected.ItbisTotal, "TotalITBIS", violations);

        CompareRate(totals.ItbisRate1, expected.ItbisRate1, "ITBIS1", violations);
        CompareRate(totals.ItbisRate2, expected.ItbisRate2, "ITBIS2", violations);
        CompareRate(totals.ItbisRate3, expected.ItbisRate3, "ITBIS3", violations);

        // The grand total carries the additional taxes, which no line can
        // account for, so they are taken the way the document declares them.
        Compare(
            totals.Total,
            expected.Total + (totals.AdditionalTaxTotal ?? 0m),
            "MontoTotal",
            violations,
            "the document adds up to");
    }

    private static void Compare(
        decimal? declared,
        decimal? expected,
        string field,
        List<EcfValidationViolation> violations,
        string expectedText = "the lines add up to")
    {
        var difference = Math.Abs((declared ?? 0m) - (expected ?? 0m));

        if (difference <= AmountTolerance)
            return;

        violations.Add(new(
            "totals",
            $"<{field}> is {Money(declared ?? 0m)} but {expectedText} {Money(expected ?? 0m)}."));
    }

    private static void CompareRate(
        int? declared,
        int? expected,
        string field,
        List<EcfValidationViolation> violations)
    {
        if (declared == expected)
            return;

        violations.Add(new(
            "totals",
            $"<{field}> says {declared?.ToString(CultureInfo.InvariantCulture) ?? "nothing"} and the lines are taxed at {expected?.ToString(CultureInfo.InvariantCulture) ?? "nothing"}."));
    }

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
}
