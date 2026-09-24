using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.SupplierDtos;

/// <summary>
/// Cycle 1.1 wire refactor — request-only DTO for supplier creation. Deliberately
/// excludes <c>CompanyId</c>: tenant scope is decided by the JWT-derived
/// <c>companyId</c> the controller passes to <c>SupplierService.CreateSupplierAsync</c>.
/// Wire-contract guard lives in <c>WireContractTests</c>.
/// </summary>
public record SupplierCreateDto:IMapTo<Supplier>
{
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
        // CompanyId is NOT copied here — SupplierService.CreateSupplierAsync sets
        // it from the companyId parameter (which the controller reads from the JWT).
        return new Domain.Entities.Supplier
        {
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