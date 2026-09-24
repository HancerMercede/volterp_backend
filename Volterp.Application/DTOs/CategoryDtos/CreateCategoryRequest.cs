using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.CategoryDtos;

/// <summary>
/// Cycle 1.1 wire refactor — request-only DTO for categories. Deliberately
/// excludes <c>CompanyId</c>: tenant scope is decided by the JWT-derived
/// <c>companyId</c> the controller passes to <c>CategoryService.CreateAsync</c>.
/// Wire-contract guard lives in <c>WireContractTests</c>.
/// </summary>
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
