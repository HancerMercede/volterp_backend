using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Volterp.Application.Common;
using Volterp.Infrastructure.Services;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Infrastructure;

public class JwtServiceTests
{
    private static JwtService BuildSut() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-signing-key-must-be-at-least-32-bytes-long-for-hmacsha256",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build());

    [Fact]
    public void GenerateToken_EmitsUserIdClaim()
    {
        var sut = BuildSut();
        var token = sut.GenerateToken("alice", "alice@test.com", "Admin",
            Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c =>
            c.Type == Claims.UserId && c.Value == Guid7TestValues.UserA.ToString("N"));
    }

    [Fact]
    public void GenerateToken_EmitsCompanyIdClaim()
    {
        var sut = BuildSut();
        var token = sut.GenerateToken("alice", "alice@test.com", "Admin",
            Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c =>
            c.Type == Claims.CompanyId && c.Value == Guid7TestValues.CompanyA.ToString("N"));
    }

    [Fact]
    public void GenerateToken_PreservesIssuerAndAudience()
    {
        var sut = BuildSut();
        var token = sut.GenerateToken("alice", "alice@test.com", "Admin",
            Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Issuer.Should().Be("test-issuer");
        jwt.Audiences.Should().Contain("test-audience");
    }

    [Fact]
    public void GenerateToken_EmitsExpectedCoreClaims()
    {
        var sut = BuildSut();
        var token = sut.GenerateToken("alice", "alice@test.com", "Admin",
            Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var claimTypes = jwt.Claims.Select(c => c.Type).ToList();

        claimTypes.Should().Contain(Claims.UserId);
        claimTypes.Should().Contain(Claims.CompanyId);
        claimTypes.Should().Contain(ClaimTypes.Name);
        claimTypes.Should().Contain(ClaimTypes.Email);
        claimTypes.Should().Contain(ClaimTypes.Role);
        claimTypes.Should().Contain(JwtRegisteredClaimNames.Jti);
    }
}