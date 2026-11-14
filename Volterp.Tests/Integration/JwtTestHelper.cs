using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Volterp.Application.Interfaces;

namespace Volterp.Tests.Integration;

public static class JwtTestHelper
{
    public static string MintToken(ApiFactory factory, Guid companyId, Guid userId, string role = "Admin")
    {
        var jwt = factory.Services.GetRequiredService<IJwtService>();
        return jwt.GenerateToken(
            username: $"{companyId:N}-user",
            email: $"{companyId:N}@test.local",
            role: role,
            companyId: companyId,
            userId: userId);
    }

    // A token of a user that does not belong to a company: the surface of
    // the tenant takes the company from the token, so this one has none.
    public static string MintTokenWithoutCompany(ApiFactory factory, Guid userId, string role = "Admin")
    {
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "user-without-company"),
            new Claim(ClaimTypes.Email, "user-without-company@test.local"),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
