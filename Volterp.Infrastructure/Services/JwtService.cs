using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Volterp.Application.Common;
using Volterp.Application.Interfaces;
using Volterp.Domain.Enums;

namespace Volterp.Infrastructure.Services;

public class JwtService(IConfiguration configuration) : IJwtService
{
    public string GenerateToken(string username, string email, IReadOnlyCollection<UserRole> roles, Guid companyId, Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Email, email),
            new(Claims.CompanyId, companyId.ToString("N")),
            new(Claims.UserId, userId.ToString("N")),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // One claim per role: the token carries every role the user holds, so
        // the server resolves the union of the permissions of the user without
        // asking the database for it.
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role.ToString())));

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(configuration["Jwt:ExpiryMinutes"]!)),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
