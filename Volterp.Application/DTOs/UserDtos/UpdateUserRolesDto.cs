using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.UserDtos;

// The roles the user ends up holding. The set is the whole truth: a role that
// is not here is taken away from the user.
public record UpdateUserRolesDto(UserRole[]? Roles);
