using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.SupplierDtos;

public record SupplierDto:IMapFrom<Supplier>
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Category { get; set; }
    public string? ContactPerson { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    public void MapFrom(Supplier source)
    {
        Id = source.Id;
        CompanyId = source.CompanyId;
        Name = source.Name;
        Email = source.Email;
        Phone = source.Phone;
        Address = source.Address;
        Category = source.Category;
        ContactPerson = source.ContactPerson;
        ImageUrl = source.ImageUrl;
        IsActive = source.IsActive;
        CreatedAt = source.CreatedAt;
        UpdatedAt = source.UpdatedAt;
    }
}
