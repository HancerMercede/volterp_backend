namespace Volterp.Application.DTOs;

public record SupplierDto(
    int Id,
    string Name,
    string Email,
    string Phone,
    string Address,
    string Category,
    string ContactPerson,
    bool IsActive,
    DateTime? CreatedAt,
    DateTime? UpdatedAt,
    Guid? CreatedBy,
    Guid? UpdatedBy
);