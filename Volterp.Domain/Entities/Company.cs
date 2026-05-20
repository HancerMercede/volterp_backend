namespace Volterp.Domain.Entities;

public class Company:IAuditEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public string Address { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();
}