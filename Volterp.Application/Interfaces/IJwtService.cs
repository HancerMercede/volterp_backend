using Volterp.Domain.Enums;

namespace Volterp.Application.Interfaces;

public interface IJwtService
{
    // The token carries one claim per role: the permissions of the user are
    // the union of the permissions of the roles it holds.
    string GenerateToken(string username, string email, IReadOnlyCollection<UserRole> roles, Guid companyId, Guid userId);
}
