namespace Volterp.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(string username, string email, string role, int companyId);
}