namespace Volterp.Application.DTOs;

public record ClientDto(
    int Id,
    string Name,
    string Email,
    string Phone,
    string Address,
    bool IsActive,
    DateTime? CreatedAt,
    DateTime? UpdatedAt,
    Guid? CreatedBy,
    Guid? UpdatedBy
);
