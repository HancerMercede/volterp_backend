using Volterp.Domain.Abstractions;

namespace Volterp.Domain.Entities;

public class Category : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsActive { get; set; } = true;
    
    public Company Company { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = new List<Product>();

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}