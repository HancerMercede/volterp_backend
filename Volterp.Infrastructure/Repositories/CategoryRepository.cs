using Microsoft.EntityFrameworkCore;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class CategoryRepository(VolterpDbContext context)
    : RepositoryBase<Category>(context), ICategoryRepository
{
    public async Task<Category?> GetCategoryByIdAsync(int id, CancellationToken ct = default)
        => await GetByCondictionsAsync(c => c.Id == id, ct);

    public async Task<PagedResult<Category>> GetAllCategoriesByCompanyAsync(int companyId, int pageNumber, int pageSize,
        CancellationToken ct = default)=>await GetAllAsync(c => c.CompanyId == companyId, pageNumber, pageSize, ct);
    
    public async Task<Category> AddCategoryAsync(Category category, CancellationToken ct = default)
    {
        await AddAsync(category, ct);
        return category;
    }

    public async Task UpdateCategoryAsync(Category category, CancellationToken ct = default)
        => await UpdateAsync(category, ct);

    public async Task DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        var category = await GetByIdAsync(id, ct);
        if (category is not null)
            await DeleteAsync(category, ct);
    }

    public async Task<bool> ExistsCategoryAsync(int id, CancellationToken ct = default)
        => await ExistsAsync(c => c.Id == id, ct);
}