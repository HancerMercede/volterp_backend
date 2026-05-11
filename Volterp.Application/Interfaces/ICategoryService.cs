using Volterp.Application.DTOs;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface ICategoryService
{
    Task<PagedResult<CategoryDto>> GetAllAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<CategoryDto?> GetByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, int companyId, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(int id, UpdateCategoryRequest request, int companyId, CancellationToken ct = default);
    Task DeleteAsync(int id, int companyId, CancellationToken ct = default);
    Task<bool> ExistsAsync(int id, CancellationToken ct = default);
}