using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.CategoryDtos;

public record CreateCategoryRequest(
    string Name,
    string? Description,
    string? Code = null
):IMapTo<Category>
{
    public Category MapTo()
    {
        // CompanyId is NOT copied here — it is set by CategoryService.CreateAsync
        // from the companyId parameter (which the controller reads from the JWT).
        // Code is copied as requested; CategoryService.CreateAsync resolves it
        // (user code or generated sequence) and validates its uniqueness.
        return new Category
        {
          Name = Name,
          Description = Description,
          Code = Code?.Trim() ?? string.Empty
        };
    }
}
