using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.CategoryDtos;

public record CreateCategoryRequest(
    string Name,
    string? Description,
    int CompanyId
):IMapTo<Category>
{
    public Category MapTo()
    {
        return new Category
        {
          Name = Name,
          Description = Description,
          CompanyId =  CompanyId
        };
    }
}
