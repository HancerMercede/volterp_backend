namespace Volterp.Application.DTOs;

public record UpdateSupplierRequest(
    string Name,
    string Email,
    string Phone,
    string Address,
    string Category,
    string ContactPerson,
    bool IsActive
);