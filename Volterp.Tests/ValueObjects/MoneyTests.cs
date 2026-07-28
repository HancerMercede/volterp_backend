using FluentAssertions;
using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;
using Xunit;

namespace Volterp.Tests.ValueObjects;

public class MoneyTests
{
    // === Creation ===

    [Fact]
    public void Money_WithPositiveAmount_ShouldCreateSuccessfully()
    {
        var money = new Money(100.50m, Currency.USD);

        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be(Currency.USD);
    }

    [Fact]
    public void Money_WithZeroAmount_ShouldCreateSuccessfully()
    {
        var money = new Money(0, Currency.ARS);

        money.Amount.Should().Be(0);
        money.Currency.Should().Be(Currency.ARS);
    }

    [Fact]
    public void Money_WithNegativeAmount_ShouldThrow()
    {
        var act = () => new Money(-50m, Currency.USD);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*negative*");
    }

    // === Addition ===

    [Fact]
    public void Money_Add_SameCurrency_ShouldReturnSum()
    {
        var a = new Money(100m, Currency.USD);
        var b = new Money(50m, Currency.USD);

        var result = a.Add(b);

        result.Amount.Should().Be(150m);
        result.Currency.Should().Be(Currency.USD);
    }

    [Fact]
    public void Money_Add_DifferentCurrency_ShouldThrow()
    {
        var a = new Money(100m, Currency.USD);
        var b = new Money(50m, Currency.ARS);

        var act = () => a.Add(b);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*currency*");
    }

    [Fact]
    public void Money_OperatorPlus_SameCurrency_ShouldReturnSum()
    {
        var a = new Money(100m, Currency.USD);
        var b = new Money(50m, Currency.USD);

        var result = a + b;

        result.Amount.Should().Be(150m);
        result.Currency.Should().Be(Currency.USD);
    }

    // === Subtraction ===

    [Fact]
    public void Money_Subtract_Valid_ShouldReturnDifference()
    {
        var a = new Money(100m, Currency.USD);
        var b = new Money(30m, Currency.USD);

        var result = a.Subtract(b);

        result.Amount.Should().Be(70m);
        result.Currency.Should().Be(Currency.USD);
    }

    [Fact]
    public void Money_Subtract_ResultNegative_ShouldReturnNegativeMoney()
    {
        var a = new Money(30m, Currency.USD);
        var b = new Money(100m, Currency.USD);

        var result = a.Subtract(b);

        result.Amount.Should().Be(-70m);
        result.Currency.Should().Be(Currency.USD);
    }

    [Fact]
    public void Money_Subtract_DifferentCurrency_ShouldThrow()
    {
        var a = new Money(100m, Currency.USD);
        var b = new Money(30m, Currency.ARS);

        var act = () => a.Subtract(b);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*currency*");
    }

    // === Equality ===

    [Fact]
    public void Money_Equals_SameValues_ShouldBeEqual()
    {
        var a = new Money(100m, Currency.USD);
        var b = new Money(100m, Currency.USD);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Money_Equals_DifferentAmount_ShouldNotBeEqual()
    {
        var a = new Money(100m, Currency.USD);
        var b = new Money(200m, Currency.USD);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Money_Equals_DifferentCurrency_ShouldNotBeEqual()
    {
        var a = new Money(100m, Currency.USD);
        var b = new Money(100m, Currency.ARS);

        a.Should().NotBe(b);
    }

    // === ToString ===

    [Fact]
    public void Money_ToString_ShouldFormatCorrectly()
    {
        var money = new Money(100.50m, Currency.USD);

        var str = money.ToString();

        str.Should().Contain("100.50").And.Contain("USD");
    }

    // === Zero constant ===

    [Fact]
    public void Money_Zero_ShouldReturnZeroAmountInDefaultCurrency()
    {
        var zero = Money.Zero;

        zero.Amount.Should().Be(0);
        zero.Currency.Should().Be(Currency.DOP);
    }
}
