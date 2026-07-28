using MapFlow;

namespace Volterp.Domain.Entities;

public class Client : AuditEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;
}
