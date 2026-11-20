using Volterp.Application.DTOs;
using Volterp.Domain.Enums;

namespace Volterp.Application.Interfaces;

// The permissions of a caller: the union of the permissions of the roles it
// holds. The server resolves the union, so the front does not keep a matrix
// of its own (S6).
public interface IPermissionService
{
    /// <summary>
    /// The permissions a caller with these roles may use, as the keys the
    /// front already knows.
    /// </summary>
    IReadOnlyCollection<string> EffectivePermissions(IEnumerable<UserRole> roles);

    /// <summary>
    /// The permissions one role grants.
    /// </summary>
    IReadOnlyCollection<string> PermissionsOf(UserRole role);

    /// <summary>
    /// The whole matrix: what every role of the catalog grants.
    /// </summary>
    IReadOnlyCollection<RolePermissionsDto> Matrix();
}
