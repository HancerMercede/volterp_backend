using FluentAssertions;
using Volterp.Domain.Enums;
using Xunit;

namespace Volterp.Tests.ValueObjects;

public class CurrencyTests
{
    [Fact]
    public void Currency_ShouldHaveFiveValues()
    {
        var values = Enum.GetValues<Currency>();
        values.Should().HaveCount(5);
    }

    [Fact]
    public void ARS_ShouldHaveValue1()
    {
        ((int)Currency.ARS).Should().Be(1);
    }

    [Fact]
    public void USD_ShouldHaveValue2()
    {
        ((int)Currency.USD).Should().Be(2);
    }

    [Fact]
    public void EUR_ShouldHaveValue3()
    {
        ((int)Currency.EUR).Should().Be(3);
    }

    [Fact]
    public void BRL_ShouldHaveValue4()
    {
        ((int)Currency.BRL).Should().Be(4);
    }

    [Fact]
    public void DOP_ShouldHaveValue5()
    {
        ((int)Currency.DOP).Should().Be(5);
    }
}