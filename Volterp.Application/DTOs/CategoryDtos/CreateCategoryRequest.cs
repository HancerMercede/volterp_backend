using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.CategoryDtos;

public record CreateCategoryRequest(
    string Name,
    string? Description
):IMapTo<Category>
{
    public Category MapTo()
    {
        // CompanyId is NOT copied here — it is set by CategoryService.CreateAsync
        // from the companyId parameter (which the controller reads from the JWT).
        return new Category
        {
          Name = Name,
          Description = Description
        };
    }
}
