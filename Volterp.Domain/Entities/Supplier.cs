using Volterp.Domain.ValueObjects;

namespace Volterp.Domain.Entities;

public class Supplier : AuditEntity
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public Company Company { get; set; } = null!;

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public void UpdateEmail(Email email) => Email = email.Value;
    public void UpdatePhone(string? phone) => Phone = phone ?? string.Empty;
}