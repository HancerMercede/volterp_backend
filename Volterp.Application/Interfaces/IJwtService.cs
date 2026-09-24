namespace Volterp.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(string username, string email, string role, Guid companyId, Guid userId);
}
