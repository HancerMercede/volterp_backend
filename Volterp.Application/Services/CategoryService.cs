using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class CategoryService(IUnitOfWork unitOfWork) : ICategoryService
{
    public async Task<PagedResult<CategoryDto>> GetAllAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var categories = await unitOfWork.Categories.GetAllCategoriesByCompanyAsync(companyId, pageNumber, pageSize,ct);

        return categories.Map(c => new CategoryDto(c.Id, c.Name, c.Description, c.CompanyId, c.IsActive, c.CreatedAt));
    }

    public async Task<CategoryDto?> GetByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        var category = await unitOfWork.Categories.GetCategoryByIdAsync(id, ct);
        
        if (category is null || category.CompanyId != companyId)
            return null;
        
        return category.Map(x=>new CategoryDto(
            x.Id, x.Name, x.Description,
            x.CompanyId, x.IsActive, x.CreatedAt
        ));
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, int companyId, CancellationToken ct = default)
    {
        var category = new Category
        {
            Name = request.Name,
            Description = request.Description,
            CompanyId = companyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await unitOfWork.Categories.AddCategoryAsync(category, ct);
        await unitOfWork.CommitAsync(ct);

        return category.Map(x=>new CategoryDto(
            x.Id, x.Name, x.Description,
            x.CompanyId, x.IsActive, x.CreatedAt
        ));
    }

    public async Task<CategoryDto> UpdateAsync(int id, UpdateCategoryRequest request, int companyId, CancellationToken ct = default)
    {
        var category = await unitOfWork.Categories.GetCategoryByIdAsync(id, ct);
        
        if (category is null || category.CompanyId != companyId)
            throw new ArgumentException("Category not found");

        category.Apply(x =>
        {
            x.Name = request.Name;
            x.Description = request.Description;
            x.IsActive = request.IsActive;
            x.UpdatedAt = DateTime.UtcNow;
        });

        await unitOfWork.Categories.UpdateCategoryAsync(category, ct);
        await unitOfWork.CommitAsync(ct);

        return category.Map(x=> new CategoryDto(
            x.Id, x.Name, x.Description,
            x.CompanyId, x.IsActive, x.CreatedAt
        ));
    }

    public async Task DeleteAsync(int id, int companyId, CancellationToken ct = default)
    {
        var category = await unitOfWork.Categories.GetCategoryByIdAsync(id, ct);
        
        if (category is null || category.CompanyId != companyId)
            throw new ArgumentException("Category not found");
        
        await unitOfWork.Categories.DeleteCategoryAsync(id, ct);
        await unitOfWork.CommitAsync(ct);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken ct = default)
    {
        return await unitOfWork.Categories.ExistsCategoryAsync(id, ct);
    }
}