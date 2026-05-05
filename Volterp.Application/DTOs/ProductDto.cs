namespace Volterp.Application.DTOs;

public record ProductDto(
    int Id,
    string Name,
    string Category,
    string? Description,
    decimal Price,
    int Stock,
    int? CategoryId,
    string? CategoryName,
    int CompanyId,
    bool IsActive,
    string? ImageUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreateProductRequest(
    string Name,
    string Category,
    string? Description,
    decimal Price,
    int Stock,
    int? CategoryId,
    int CompanyId,
    string? ImageUrl = null
);

public record UpdateProductRequest(
    string Name,
    string Category,
    string? Description,
    decimal Price,
    int Stock,
    int? CategoryId,
    bool IsActive,
    string? ImageUrl = null
);