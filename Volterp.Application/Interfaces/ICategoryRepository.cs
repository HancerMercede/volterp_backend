using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface ICategoryRepository : IRepositoryBase<Category>
{
    Task<Category?> GetCategoryByIdAsync(int id, CancellationToken ct = default);
    Task<PagedResult<Category>> GetAllCategoriesByCompanyAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Category> AddCategoryAsync(Category category, CancellationToken ct = default);
    Task UpdateCategoryAsync(Category category, CancellationToken ct = default);
    Task DeleteCategoryAsync(int id, CancellationToken ct = default);
    Task<bool> ExistsCategoryAsync(int id, CancellationToken ct = default);
}