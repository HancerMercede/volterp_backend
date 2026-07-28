using FluentAssertions;
using Volterp.Domain.ValueObjects;
using Xunit;

namespace Volterp.Tests.ValueObjects;

public class EmailTests
{
    // === Creation ===

    [Fact]
    public void Email_WithValidAddress_ShouldCreateSuccessfully()
    {
        var email = new Email("user@example.com");

        email.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void Email_WithSubdomain_ShouldCreateSuccessfully()
    {
        var email = new Email("user@sub.example.com");

        email.Value.Should().Be("user@sub.example.com");
    }

    [Fact]
    public void Email_WithPlusTag_ShouldCreateSuccessfully()
    {
        var email = new Email("user+tag@example.com");

        email.Value.Should().Be("user+tag@example.com");
    }

    // === Invalid inputs ===

    [Fact]
    public void Email_WithMissingAtSymbol_ShouldThrow()
    {
        var act = () => new Email("userexample.com");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*email*");
    }

    [Fact]
    public void Email_WithMissingDomain_ShouldThrow()
    {
        var act = () => new Email("user@");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*email*");
    }

    [Fact]
    public void Email_WithNull_ShouldThrow()
    {
        var act = () => new Email(null!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*email*");
    }

    [Fact]
    public void Email_WithEmptyString_ShouldThrow()
    {
        var act = () => new Email("");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*email*");
    }

    [Fact]
    public void Email_WithWhitespace_ShouldThrow()
    {
        var act = () => new Email("   ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*email*");
    }

    // === Equality ===

    [Fact]
    public void Email_Equals_SameValue_ShouldBeEqual()
    {
        var a = new Email("user@example.com");
        var b = new Email("user@example.com");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Email_Equals_DifferentValue_ShouldNotBeEqual()
    {
        var a = new Email("user@example.com");
        var b = new Email("other@example.com");

        a.Should().NotBe(b);
    }

    // === ToString ===

    [Fact]
    public void Email_ToString_ShouldReturnValue()
    {
        var email = new Email("user@example.com");

        email.ToString().Should().Be("user@example.com");
    }
}
