using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.SupplierDtos;

public record UpdateSupplierRequest
{
    public int Id { get; set; }

    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Category { get; set; }
    public string? ContactPerson { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? UpdatedAt { get; set; }
}
