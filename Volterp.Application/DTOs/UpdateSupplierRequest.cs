namespace Volterp.Application.DTOs;

public record UpdateSupplierRequest(
    int Id,
    int CompanyId,
    string Name,
    string Email,
    string Phone,
    string Address,
    string Category,
    string ContactPerson,
    bool IsActive
);