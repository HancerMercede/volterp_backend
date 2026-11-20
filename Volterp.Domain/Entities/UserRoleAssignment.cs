using Volterp.Domain.Abstractions;
using Volterp.Domain.Enums;

namespace Volterp.Domain.Entities;

// A role the user holds. A user holds many roles, and the effective
// permissions of the user are the union of the permissions of every one of
// them: the server resolves that union and the front keeps no copy of it.
public class UserRoleAssignment : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserRole Role { get; set; }
    public Guid CompanyId { get; set; }

    public User User { get; set; } = null!;
    public Company Company { get; set; } = null!;
}
