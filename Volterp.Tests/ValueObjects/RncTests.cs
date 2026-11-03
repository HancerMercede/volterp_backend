using FluentAssertions;
using Volterp.Domain.Enums;
using Volterp.Domain.Exceptions;
using Volterp.Domain.ValueObjects;
using Xunit;

namespace Volterp.Tests.ValueObjects;

public class RncTests
{
    // === Company RNC (9 digits) ===

    [Fact]
    public void Rnc_WithValidCompanyRnc_ShouldCreateSuccessfully()
    {
        var rnc = new Rnc("130862346");

        rnc.Value.Should().Be("130862346");
        rnc.Type.Should().Be(RncType.PersonaJuridica);
    }

    [Fact]
    public void Rnc_WithCompanyRncWhoseRemainderIsOne_ShouldCreateSuccessfully()
    {
        var rnc = new Rnc("132620951");

        rnc.Value.Should().Be("132620951");
        rnc.Type.Should().Be(RncType.PersonaJuridica);
    }

    [Fact]
    public void Rnc_WithCompanyRncWhoseRemainderIsZero_ShouldCreateSuccessfully()
    {
        var rnc = new Rnc("131123432");

        rnc.Value.Should().Be("131123432");
        rnc.Type.Should().Be(RncType.PersonaJuridica);
    }

    [Fact]
    public void Rnc_WithInvalidCompanyCheckDigit_ShouldThrow()
    {
        var act = () => new Rnc("131123456");

        act.Should().Throw<InvalidRncException>()
            .WithMessage("*RNC*");
    }

    // === Citizen ID (11 digits) ===

    [Fact]
    public void Rnc_WithValidCitizenId_ShouldCreateSuccessfully()
    {
        var rnc = new Rnc("04800009575");

        rnc.Value.Should().Be("04800009575");
        rnc.Type.Should().Be(RncType.PersonaFisica);
    }

    [Fact]
    public void Rnc_WithHistoricalCitizenId_ShouldCreateSuccessfully()
    {
        var rnc = new Rnc("40200700675");

        rnc.Value.Should().Be("40200700675");
        rnc.Type.Should().Be(RncType.PersonaFisica);
    }

    [Fact]
    public void Rnc_WithInvalidCitizenCheckDigit_ShouldThrow()
    {
        var act = () => new Rnc("04800009577");

        act.Should().Throw<InvalidRncException>()
            .WithMessage("*RNC*");
    }

    // === Normalization ===

    [Fact]
    public void Rnc_WithSeparatedCompanyRnc_ShouldNormalizeToDigits()
    {
        var rnc = new Rnc("1-30-86234-6");

        rnc.Value.Should().Be("130862346");
    }

    [Fact]
    public void Rnc_WithSeparatedCitizenId_ShouldNormalizeToDigits()
    {
        var rnc = new Rnc("001-0000000-9");

        rnc.Value.Should().Be("00100000009");
    }

    // === Invalid inputs ===

    [Fact]
    public void Rnc_WithThirteenDigits_ShouldThrow()
    {
        var act = () => new Rnc("1234567890123");

        act.Should().Throw<InvalidRncException>()
            .WithMessage("*RNC*");
    }

    [Fact]
    public void Rnc_WithLetters_ShouldThrow()
    {
        var act = () => new Rnc("ABC123456");

        act.Should().Throw<InvalidRncException>()
            .WithMessage("*RNC*");
    }

    [Fact]
    public void Rnc_WithNull_ShouldThrow()
    {
        var act = () => new Rnc(null!);

        act.Should().Throw<InvalidRncException>()
            .WithMessage("*RNC*");
    }

    [Fact]
    public void Rnc_WithEmptyString_ShouldThrow()
    {
        var act = () => new Rnc("");

        act.Should().Throw<InvalidRncException>()
            .WithMessage("*RNC*");
    }

    // === TryParse ===

    [Fact]
    public void Rnc_TryParse_WithValidValue_ShouldReturnTrueAndNormalize()
    {
        var result = Rnc.TryParse("1-30-86234-6", out var rnc);

        result.Should().BeTrue();
        rnc.Value.Should().Be("130862346");
    }

    [Fact]
    public void Rnc_TryParse_WithInvalidValue_ShouldReturnFalse()
    {
        var result = Rnc.TryParse("131123456", out var rnc);

        result.Should().BeFalse();
        rnc.Value.Should().BeNull();
    }

    // === Equality ===

    [Fact]
    public void Rnc_Equals_SameValue_ShouldBeEqual()
    {
        var a = new Rnc("130862346");
        var b = new Rnc("1-30-86234-6");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    // === ToString ===

    [Fact]
    public void Rnc_ToString_ShouldReturnValue()
    {
        var rnc = new Rnc("130862346");

        rnc.ToString().Should().Be("130862346");
    }
}
