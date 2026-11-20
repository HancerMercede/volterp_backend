using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Volterp.Domain.Authorization;
using Volterp.Domain.Enums;

namespace Volterp.Api.Authorization;

// The authorization of a request: the roles the token carries are read, the
// permissions of those roles are unioned, and the endpoint is let in when
// the permission it declared is among them.
public class PermissionRequirementHandler(IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (RolePermissions.For(RolesOf(context.User)).Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
        else
        {
            // The caller does not have it: the refusal names the permission
            // that is missing, because a caller that does not know which one
            // is missing cannot do anything with the refusal.
            httpContextAccessor.HttpContext?.Items[PermissionRequirement.RequiredPermissionItem] =
                requirement.Permission.Key();
        }

        return Task.CompletedTask;
    }

    // The roles the caller holds. The token carries one claim per role, and a
    // claim that is not a role of the catalog is ignored: a caller with a
    // role nobody knows holds no permissions.
    private static List<UserRole> RolesOf(ClaimsPrincipal user)
        => user.FindAll(ClaimTypes.Role)
            .Select(claim => Enum.TryParse<UserRole>(claim.Value, ignoreCase: true, out var role)
                ? role
                : (UserRole?)null)
            .Where(role => role.HasValue)
            .Select(role => role!.Value)
            .ToList();
}
