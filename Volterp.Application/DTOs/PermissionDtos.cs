using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs;

// What the caller may do: the roles it holds and the union of the
// permissions of those roles. The front reads this and stops keeping a
// matrix of its own (S6).
public record MePermissionsDto(UserRole[] Roles, string[] Permissions);

// What one role of the catalog grants.
public record RolePermissionsDto(UserRole Role, string[] Permissions);

// The whole matrix: what every role grants.
public record RolesPermissionsDto(RolePermissionsDto[] Roles);
