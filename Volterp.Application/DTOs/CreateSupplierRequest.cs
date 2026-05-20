namespace Volterp.Application.DTOs;

public record CreateSupplierRequest(
    int CompanyId,
    string Name,
    string Email,
    string Phone,
    string Address,
    string Category,
    string ContactPerson
);