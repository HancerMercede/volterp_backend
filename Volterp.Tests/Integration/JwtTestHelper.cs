using Microsoft.Extensions.DependencyInjection;
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
}