namespace Volterp.Domain.Entities;

public class Category : AuditEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsActive { get; set; } = true;
    
    public Company Company { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = new List<Product>();

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}