using Volterp.Domain.Abstractions;
using Volterp.Domain.Enums;
using Volterp.Domain.Exceptions;
using Volterp.Domain.ValueObjects;

namespace Volterp.Domain.Entities;

public class User : AuditEntity, ITenantScoped
{
    // The name the exception carries when a user has no role at all.
    public const string NoRole = "(none)";

    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    // The roles the user holds. The permissions of the user are the union of
    // the permissions of these roles.
    public ICollection<UserRoleAssignment> Roles { get; set; } = new List<UserRoleAssignment>();

    public bool IsActive { get; set; } = true;
    public Guid CompanyId { get; set; }
    
    public DateTime? LastLoginAt { get; set; }

    public Company Company { get; set; } = null!;

    public bool HasRole(UserRole role) => Roles.Any(assignment => assignment.Role == role);

    // A role is added to the ones the user already holds. A user that already
    // holds it keeps a single assignment of it.
    public void AssignRole(UserRole role)
    {
        EnsureRolesCanChange(role);

        if (HasRole(role)) return;

        Roles.Add(new UserRoleAssignment
        {
            Role = role,
            CompanyId = CompanyId,
            UserId = Id
        });
    }

    // A role is taken away from the user. The last one is never taken away: a
    // user without roles has no permissions and would be locked out of the
    // system with no way to get its access back.
    public void RemoveRole(UserRole role)
    {
        EnsureRolesCanChange(role);

        var assignment = Roles.FirstOrDefault(current => current.Role == role);

        if (assignment is null) return;

        if (Roles.Count == 1)
            throw new InvalidRoleTransitionException(role.ToString(), NoRole);

        Roles.Remove(assignment);
    }

    public void UpdateEmail(Email email)
    {
        Email = email.Value;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    // The roles of a user change only while the user is active: an inactive
    // user keeps the roles it had.
    private void EnsureRolesCanChange(UserRole role)
    {
        if (!IsActive)
            throw new InvalidRoleTransitionException(RoleNames(), role.ToString());
    }

    private string RoleNames() => Roles.Count == 0
        ? NoRole
        : string.Join(", ", Roles.Select(assignment => assignment.Role.ToString()));
}
