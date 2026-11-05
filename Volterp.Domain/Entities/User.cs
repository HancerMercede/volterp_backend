using Volterp.Domain.Abstractions;
using Volterp.Domain.Enums;
using Volterp.Domain.Exceptions;
using Volterp.Domain.ValueObjects;

namespace Volterp.Domain.Entities;

public class User : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Ventas;
    public bool IsActive { get; set; } = true;
    public Guid CompanyId { get; set; }
    
    public DateTime? LastLoginAt { get; set; }

    public Company Company { get; set; } = null!;

    public void ChangeRole(UserRole newRole)
    {
        if (!IsActive)
            throw new InvalidRoleTransitionException(Role.ToString(), newRole.ToString());

        if (Role == UserRole.SuperAdmin && newRole != UserRole.SuperAdmin)
            throw new InvalidRoleTransitionException(Role.ToString(), newRole.ToString());

        if (Role != UserRole.SuperAdmin && newRole == UserRole.SuperAdmin)
            throw new InvalidRoleTransitionException(Role.ToString(), newRole.ToString());

        Role = newRole;
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
}
