using FluentAssertions;
using Volterp.Domain.Exceptions;
using Volterp.Domain.ValueObjects;
using Xunit;

namespace Volterp.Tests.ValueObjects;

public class TaxIdTests
{
    // === Creation ===

    [Fact]
    public void TaxId_WithValidAlphanumeric_ShouldCreateSuccessfully()
    {
        var taxId = new TaxId("ABC12345");

        taxId.Value.Should().Be("ABC12345");
    }

    [Fact]
    public void TaxId_WithMaxLength_ShouldCreateSuccessfully()
    {
        var taxId = new TaxId("ABCDEF123456789");

        taxId.Value.Should().Be("ABCDEF123456789");
        taxId.Value.Length.Should().Be(15);
    }

    [Fact]
    public void TaxId_WithMinLength_ShouldCreateSuccessfully()
    {
        var taxId = new TaxId("12345678");

        taxId.Value.Should().Be("12345678");
        taxId.Value.Length.Should().Be(8);
    }

    [Fact]
    public void TaxId_WithLowercase_ShouldPreserveCase()
    {
        var taxId = new TaxId("abc12345");

        taxId.Value.Should().Be("abc12345");
    }

    [Fact]
    public void TaxId_WithSurroundingWhitespace_ShouldThrow()
    {
        var act = () => new TaxId(" ABC12345 ");

        act.Should().Throw<InvalidTaxIdException>()
            .WithMessage("*Tax ID*");
    }

    // === Invalid inputs ===

    [Fact]
    public void TaxId_WithSpecialCharacters_ShouldThrow()
    {
        var act = () => new TaxId("ABC-1234");

        act.Should().Throw<InvalidTaxIdException>()
            .WithMessage("*Tax ID*");
    }

    [Fact]
    public void TaxId_TooShort_ShouldThrow()
    {
        var act = () => new TaxId("ABC1234");

        act.Should().Throw<InvalidTaxIdException>()
            .WithMessage("*Tax ID*");
    }

    [Fact]
    public void TaxId_TooLong_ShouldThrow()
    {
        var act = () => new TaxId("ABCDEF1234567890");

        act.Should().Throw<InvalidTaxIdException>()
            .WithMessage("*Tax ID*");
    }

    [Fact]
    public void TaxId_WithNull_ShouldThrow()
    {
        var act = () => new TaxId(null!);

        act.Should().Throw<InvalidTaxIdException>()
            .WithMessage("*Tax ID*");
    }

    [Fact]
    public void TaxId_WithEmptyString_ShouldThrow()
    {
        var act = () => new TaxId("");

        act.Should().Throw<InvalidTaxIdException>()
            .WithMessage("*Tax ID*");
    }

    // === Equality ===

    [Fact]
    public void TaxId_Equals_SameValue_ShouldBeEqual()
    {
        var a = new TaxId("ABC12345");
        var b = new TaxId("ABC12345");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void TaxId_Equals_CaseSensitive_ShouldNotBeEqual()
    {
        var a = new TaxId("ABC12345");
        var b = new TaxId("abc12345");

        a.Should().NotBe(b);
    }

    [Fact]
    public void TaxId_Equals_DifferentValue_ShouldNotBeEqual()
    {
        var a = new TaxId("ABC12345");
        var b = new TaxId("XYZ67890");

        a.Should().NotBe(b);
    }

    // === ToString ===

    [Fact]
    public void TaxId_ToString_ShouldReturnValue()
    {
        var taxId = new TaxId("ABC12345");

        taxId.ToString().Should().Be("ABC12345");
    }
}