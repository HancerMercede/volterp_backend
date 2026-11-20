using System.Security.Claims;
using Volterp.Domain.Enums;

namespace Volterp.Api.Authorization;

// The roles a caller holds. The token carries one claim per role, and a
// claim that is not a role of the catalog is ignored: a caller with a role
// nobody knows holds no permissions.
public static class CallerRoles
{
    public static List<UserRole> Of(ClaimsPrincipal user)
        => user.FindAll(ClaimTypes.Role)
            .Select(claim => Enum.TryParse<UserRole>(claim.Value, ignoreCase: true, out var role)
                ? role
                : (UserRole?)null)
            .Where(role => role.HasValue)
            .Select(role => role!.Value)
            .ToList();
}
