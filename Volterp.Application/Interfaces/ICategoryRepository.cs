using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface ICategoryRepository : IRepositoryBase<Category>
{
    Task<Category?> GetCategoryByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<Category>> GetAllCategoriesAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Category> AddCategoryAsync(Category category, CancellationToken ct = default);
    Task UpdateCategoryAsync(Category category, CancellationToken ct = default);
    Task DeleteCategoryAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsCategoryAsync(Guid id, CancellationToken ct = default);
}
