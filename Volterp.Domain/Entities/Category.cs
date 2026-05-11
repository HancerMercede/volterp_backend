namespace Volterp.Domain.Entities;

public class Category:IAuditEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CompanyId { get; set; }
    public bool IsActive { get; set; } = true;
   
    public Company Company { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}