using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs;

public record SupplierCreateDto:IMapTo<Supplier>
{
    public int CompanyId { get; set; }
    public string? Name { get; set; } 
    public string? Email { get; set; } 
    public string? Phone { get; set; } 
    public string? Address { get; set; }
    public string? Category { get; set; } 
    public string? ContactPerson { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }


    public Supplier MapTo()
    {
        return new Supplier
        {
            CompanyId = CompanyId,
            Name = Name,
            Email = Email,
            Phone = Phone,
            Address = Address,
            Category = Category,
            ContactPerson = ContactPerson,
            ImageUrl = ImageUrl,
            IsActive = IsActive,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}