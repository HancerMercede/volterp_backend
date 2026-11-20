using Volterp.Application.DTOs;
using Volterp.Application.Interfaces;
using Volterp.Domain.Authorization;
using Volterp.Domain.Enums;

namespace Volterp.Application.Services;

public class PermissionService : IPermissionService
{
    // The permissions of the roles a caller holds: the union of them. The
    // union lives in the domain, so the filter and this service answer the
    // same thing.
    public IReadOnlyCollection<string> EffectivePermissions(IEnumerable<UserRole> roles)
        => RolePermissions.For(roles)
            .Select(permission => permission.Key())
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyCollection<string> PermissionsOf(UserRole role)
        => RolePermissions.For(role)
            .Select(permission => permission.Key())
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyCollection<RolePermissionsDto> Matrix()
        => Enum.GetValues<UserRole>()
            .Select(role => new RolePermissionsDto(role, PermissionsOf(role).ToArray()))
            .ToArray();
}
