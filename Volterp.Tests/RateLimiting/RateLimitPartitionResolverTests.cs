using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Volterp.Api.RateLimiting;
using Xunit;

namespace Volterp.Tests.RateLimiting;

public class RateLimitPartitionResolverTests
{
    private readonly RateLimitPartitionResolver _sut = new();

    [Fact]
    public void Anonymous_ReturnsIpKey()
    {
        var ctx = NewContext(ip: "203.0.113.7");
        _sut.GetGlobalPartitionKey(ctx).Should().Be("ip:203.0.113.7");
    }

    [Fact]
    public void DifferentIps_DifferentPartitionKeys()
    {
        var a = NewContext(ip: "203.0.113.7");
        var b = NewContext(ip: "203.0.113.8");
        _sut.GetGlobalPartitionKey(a).Should().NotBe(_sut.GetGlobalPartitionKey(b));
    }

    [Fact]
    public void Authenticated_UserIdClaim_TakesPriorityOverName()
    {
        var ctx = NewContext(ip: "203.0.113.7",
            user: NewUser(("userId", "42"), (ClaimTypes.Name, "alice")));
        _sut.GetGlobalPartitionKey(ctx).Should().Be("user:42");
    }

    [Fact]
    public void Authenticated_NameIdentifier_UsedWhenNoUserId()
    {
        var ctx = NewContext(ip: "203.0.113.7",
            user: NewUser((ClaimTypes.NameIdentifier, "alice-id"), (ClaimTypes.Name, "alice")));
        _sut.GetGlobalPartitionKey(ctx).Should().Be("user:alice-id");
    }

    [Fact]
    public void Authenticated_Name_UsedWhenNoUserIdOrNameIdentifier()
    {
        var ctx = NewContext(ip: "203.0.113.7",
            user: NewUser((ClaimTypes.Name, "alice")));
        _sut.GetGlobalPartitionKey(ctx).Should().Be("user:alice");
    }

    [Fact]
    public void SameUserDifferentIps_SamePartitionKey()
    {
        var a = NewContext(ip: "203.0.113.7", user: NewUser((ClaimTypes.Name, "alice")));
        var b = NewContext(ip: "198.51.100.10", user: NewUser((ClaimTypes.Name, "alice")));
        _sut.GetGlobalPartitionKey(a).Should().Be(_sut.GetGlobalPartitionKey(b));
    }

    [Fact]
    public void NormalizesIPv4MappedToIPv4()
    {
        var ctx = NewContext(ipAddress: IPAddress.Parse("::ffff:203.0.113.7"));
        _sut.GetGlobalPartitionKey(ctx).Should().Be("ip:203.0.113.7");
    }

    [Fact]
    public void AuthenticationPartition_AlwaysIp_EvenWhenAuthenticated()
    {
        var ctx = NewContext(ip: "203.0.113.7", user: NewUser((ClaimTypes.Name, "alice")));
        _sut.GetAuthenticationPartitionKey(ctx).Should().Be("ip:203.0.113.7");
    }

    private static DefaultHttpContext NewContext(
        string? ip = null,
        IPAddress? ipAddress = null,
        ClaimsPrincipal? user = null)
    {
        var ctx = new DefaultHttpContext
        {
            Connection = { RemoteIpAddress = ipAddress ?? (ip is null ? null : IPAddress.Parse(ip)) },
            User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
        };
        return ctx;
    }

    private static ClaimsPrincipal NewUser(params (string type, string value)[] claims)
        => new(new ClaimsIdentity(claims.Select(c => new Claim(c.type, c.value)), authenticationType: "Test"));
}