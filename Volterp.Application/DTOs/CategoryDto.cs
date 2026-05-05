namespace Volterp.Application.DTOs;

public record CategoryDto(
    int Id,
    string Name,
    string? Description,
    int CompanyId,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateCategoryRequest(
    string Name,
    string? Description,
    int CompanyId
);

public record UpdateCategoryRequest(
    string Name,
    string? Description,
    bool IsActive
);