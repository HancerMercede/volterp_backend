using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Volterp.Api.Configuration;
using Xunit;

namespace Volterp.Tests.RateLimiting;

public class RateLimitingOptionsTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        var options = new RateLimitingOptions();
        var act = () => Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0, 60, "Authenticated.PermitLimit")]
    [InlineData(-1, 60, "Authenticated.PermitLimit")]
    public void InvalidPermitLimit_Throws(int permit, int window, string expectedField)
    {
        var options = new RateLimitingOptions
        {
            Authenticated = new RateLimitPolicyOptions { PermitLimit = permit, WindowSeconds = window }
        };
        var act = () => Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        act.Should().Throw<ValidationException>().WithMessage($"*{expectedField}*");
    }

    [Theory]
    [InlineData(60, 0, "Anonymous.WindowSeconds")]
    [InlineData(60, -1, "Anonymous.WindowSeconds")]
    public void InvalidWindowSeconds_Throws(int permit, int window, string expectedField)
    {
        var options = new RateLimitingOptions
        {
            Anonymous = new RateLimitPolicyOptions { PermitLimit = permit, WindowSeconds = window }
        };
        var act = () => Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        act.Should().Throw<ValidationException>().WithMessage($"*{expectedField}*");
    }
}