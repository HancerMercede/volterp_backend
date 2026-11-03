using FluentAssertions;
using Volterp.Domain.Exceptions;
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

        act.Should().Throw<InvalidEmailException>()
            .WithMessage("*email*");
    }

    [Fact]
    public void Email_WithMissingDomain_ShouldThrow()
    {
        var act = () => new Email("user@");

        act.Should().Throw<InvalidEmailException>()
            .WithMessage("*email*");
    }

    [Fact]
    public void Email_WithNull_ShouldThrow()
    {
        var act = () => new Email(null!);

        act.Should().Throw<InvalidEmailException>()
            .WithMessage("*email*");
    }

    [Fact]
    public void Email_WithEmptyString_ShouldThrow()
    {
        var act = () => new Email("");

        act.Should().Throw<InvalidEmailException>()
            .WithMessage("*email*");
    }

    [Fact]
    public void Email_WithSurroundingWhitespace_ShouldThrow()
    {
        var act = () => new Email(" user@example.com ");

        act.Should().Throw<InvalidEmailException>()
            .WithMessage("*email*");
    }

    // === ToString ===

    [Fact]
    public void Email_ToString_ShouldReturnValue()
    {
        var email = new Email("user@example.com");

        email.ToString().Should().Be("user@example.com");
    }
}