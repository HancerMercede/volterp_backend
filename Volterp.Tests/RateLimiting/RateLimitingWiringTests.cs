using System.Net;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Api.Controllers;
using Volterp.Api.Helpers;
using Volterp.Api.RateLimiting;
using Xunit;

namespace Volterp.Tests.RateLimiting;

public class RateLimitingWiringTests
{
    [Fact]
    public void AuthController_HasEnableRateLimitingAttributeOnClass()
    {
        var attribute = typeof(AuthController).GetCustomAttribute<EnableRateLimitingAttribute>(inherit: true);
        attribute.Should().NotBeNull();
        attribute!.PolicyName.Should().Be(RateLimiterPolicies.Authentication);
    }

    [Theory]
    [InlineData("Register")]
    [InlineData("Login")]
    public void AuthController_Action_InheritsAuthRateLimitPolicy(string actionName)
    {
        var action = typeof(AuthController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Single(m => m.Name == actionName);

        action.GetCustomAttribute<HttpPostAttribute>(inherit: true).Should().NotBeNull();

        var attr = action.GetCustomAttribute<EnableRateLimitingAttribute>(inherit: true)
                   ?? typeof(AuthController).GetCustomAttribute<EnableRateLimitingAttribute>(inherit: true);

        attr.Should().NotBeNull();
        attr!.PolicyName.Should().Be(RateLimiterPolicies.Authentication);
    }

    [Fact]
    public void ConfigureForwardedHeaders_TrustsLoopbackOnly_AndPreservesFlags()
    {
        var services = new ServiceCollection();
        services.ConfigureForwardedHeaders();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ForwardedHeadersOptions>>().Value;

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        options.ForwardLimit.Should().Be(1);
        options.KnownProxies.Should().Contain(IPAddress.Loopback);
        options.KnownProxies.Should().Contain(IPAddress.IPv6Loopback);
        options.KnownProxies.Should().NotContain(IPAddress.Parse("203.0.113.10"));
    }

    [Fact]
    public void ConfigureForwardedHeaders_IsIdempotentForLoopback()
    {
        var services = new ServiceCollection();
        services.ConfigureForwardedHeaders();
        services.ConfigureForwardedHeaders();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ForwardedHeadersOptions>>().Value;

        options.KnownProxies.Count(p => p.Equals(IPAddress.Loopback)).Should().Be(1);
        options.KnownProxies.Count(p => p.Equals(IPAddress.IPv6Loopback)).Should().Be(1);
    }
}