using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.CategoryDtos;

public record UpdateCategoryRequest(
    string Name,
    string? Description,
    bool IsActive
):IMapTo<Category>
{
    public Category MapTo()
    {
        return new Category
        {
            Name = Name,
            Description = Description,
            IsActive = IsActive
        };
    }
}