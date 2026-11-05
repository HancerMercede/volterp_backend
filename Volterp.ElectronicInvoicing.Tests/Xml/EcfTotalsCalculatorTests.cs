using FluentAssertions;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Xml;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Xml;

public class EcfTotalsCalculatorTests
{
    [Fact]
    public void Calculate_GroupsTheTaxableAmountByRate()
    {
        var items = new[]
        {
            Item(TaxCode.ItbisTasa1, 1000m, 1),
            Item(TaxCode.ItbisTasa2, 500m, 2),
            Item(TaxCode.ItbisTasa3, 200m, 3),
            Item(TaxCode.Exento, 100m, 4),
            Item(TaxCode.NoFacturable, 50m, 5)
        };

        var totals = EcfTotalsCalculator.Calculate(items);

        totals.TaxableTotal.Should().Be(1700m);
        totals.TaxableAtRate1.Should().Be(1000m);
        totals.TaxableAtRate2.Should().Be(500m);
        totals.TaxableAtRate3.Should().Be(200m);
        totals.ExemptTotal.Should().Be(100m);
        totals.NonBillableTotal.Should().Be(50m);
    }

    [Fact]
    public void Calculate_EmitsTheRateTheDgiiCodesForEveryBucket()
    {
        var items = new[]
        {
            Item(TaxCode.ItbisTasa1, 1000m, 1),
            Item(TaxCode.ItbisTasa2, 500m, 2),
            Item(TaxCode.ItbisTasa3, 200m, 3)
        };

        var totals = EcfTotalsCalculator.Calculate(items);

        totals.ItbisRate1.Should().Be(18);
        totals.ItbisRate2.Should().Be(16);
        totals.ItbisRate3.Should().Be(0);
    }

    [Fact]
    public void Calculate_SplitsTheTaxByRate()
    {
        var items = new[]
        {
            Item(TaxCode.ItbisTasa1, 1000m, 1),
            Item(TaxCode.ItbisTasa2, 500m, 2)
        };

        var totals = EcfTotalsCalculator.Calculate(items);

        totals.ItbisAtRate1.Should().Be(180m);
        totals.ItbisAtRate2.Should().Be(80m);
        totals.ItbisTotal.Should().Be(260m);
    }

    [Fact]
    public void Calculate_LeavesOutTheBucketsThatHaveNoLines()
    {
        var items = new[] { Item(TaxCode.ItbisTasa1, 1000m, 1) };

        var totals = EcfTotalsCalculator.Calculate(items);

        totals.TaxableAtRate2.Should().BeNull();
        totals.TaxableAtRate3.Should().BeNull();
        totals.ExemptTotal.Should().BeNull();
        totals.NonBillableTotal.Should().BeNull();
        totals.ItbisRate2.Should().BeNull();
        totals.ItbisRate3.Should().BeNull();
        totals.ItbisAtRate2.Should().BeNull();
    }

    [Fact]
    public void Calculate_RoundsTheTaxOfEveryLineOnItsOwn()
    {
        var items = new[]
        {
            Item(TaxCode.ItbisTasa1, 0.04m, 1),
            Item(TaxCode.ItbisTasa1, 0.04m, 2),
            Item(TaxCode.ItbisTasa1, 0.04m, 3)
        };

        var totals = EcfTotalsCalculator.Calculate(items);

        // Every line rounds to 0.01. Rounding the sum instead would give 0.02.
        totals.ItbisTotal.Should().Be(0.03m);
    }

    [Fact]
    public void Calculate_KeepsTheNonBillableAmountOutOfTheTotal()
    {
        var items = new[]
        {
            Item(TaxCode.ItbisTasa1, 1000m, 1),
            Item(TaxCode.NoFacturable, 50m, 2)
        };

        var totals = EcfTotalsCalculator.Calculate(items);

        totals.NonBillableTotal.Should().Be(50m);
        totals.Total.Should().Be(1180m);
    }

    [Fact]
    public void Calculate_CountsTheExemptAmountInTheTotal()
    {
        var items = new[]
        {
            Item(TaxCode.ItbisTasa1, 1000m, 1),
            Item(TaxCode.Exento, 100m, 2)
        };

        var totals = EcfTotalsCalculator.Calculate(items);

        totals.Total.Should().Be(1280m);
    }

    [Fact]
    public void Calculate_OfNoLines_IsZero()
    {
        var totals = EcfTotalsCalculator.Calculate([]);

        totals.TaxableTotal.Should().Be(0m);
        totals.ItbisTotal.Should().Be(0m);
        totals.Total.Should().Be(0m);
    }

    private static EcfItem Item(TaxCode taxCode, decimal amount, int lineNumber) => new()
    {
        LineNumber = lineNumber,
        TaxTreatment = taxCode,
        Name = "Producto de prueba",
        Kind = EcfItemKind.Goods,
        Quantity = 1m,
        UnitPrice = amount,
        Amount = amount
    };
}
