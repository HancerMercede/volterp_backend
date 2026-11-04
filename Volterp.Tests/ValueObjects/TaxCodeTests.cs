using FluentAssertions;
using Xunit;
using Volterp.ElectronicInvoicing;

namespace Volterp.Tests.ValueObjects;

public class TaxCodeTests
{
    [Theory]
    [InlineData(TaxCode.NoFacturable, 0)]
    [InlineData(TaxCode.ItbisTasa1, 1)]
    [InlineData(TaxCode.ItbisTasa2, 2)]
    [InlineData(TaxCode.ItbisTasa3, 3)]
    [InlineData(TaxCode.Exento, 4)]
    public void TaxCode_MatchesDgiiIndicadorFacturacion(TaxCode code, int expected)
    {
        ((int)code).Should().Be(expected);
    }
}
