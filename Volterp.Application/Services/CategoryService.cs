using Volterp.Application.DTOs;
using Volterp.Application.DTOs.CategoryDtos;
using Volterp.Application.Exceptions.Category;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class CategoryService(IUnitOfWork unitOfWork) : ICategoryService
{
    public async Task<PagedResult<CategoryDto>> GetAllAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var categories = await unitOfWork.Categories.GetAllCategoriesByCompanyAsync(companyId, pageNumber, pageSize,ct);

        return categories.MapTo<Category, CategoryDto>();
    }

    public async Task<CategoryDto?> GetByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        var category = await unitOfWork.Categories.GetCategoryByIdAsync(id, ct);
        
        if (category is null || category.CompanyId != companyId)
            return null;

        return category.MapTo<Category, CategoryDto>();
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, int companyId, CancellationToken ct = default)
    {
        var category = request.Project();
        category.CompanyId = companyId;
        
        await unitOfWork.Categories.AddCategoryAsync(category, ct);
        await unitOfWork.CommitAsync(ct);

        return category.MapTo<Category, CategoryDto>();
    }

    public async Task<CategoryDto> UpdateAsync(int id, UpdateCategoryRequest request, int companyId, CancellationToken ct = default)
    {
        var category = await unitOfWork.Categories.GetCategoryByIdAsync(id, ct);
        
        if (category is null || category.CompanyId != companyId)
            throw new CategoryNotFoundException("Category not found");

        category.Apply(x =>
        {
            x.Name = request.Name;
            x.Description = request.Description;
            x.IsActive = request.IsActive;
            x.UpdatedAt = DateTime.UtcNow;
        });

        await unitOfWork.Categories.UpdateCategoryAsync(category, ct);
        await unitOfWork.CommitAsync(ct);

        return category.MapTo<Category, CategoryDto>();
    }

    public async Task DeleteAsync(int id, int companyId, CancellationToken ct = default)
    {
        var category = await unitOfWork.Categories.GetCategoryByIdAsync(id, ct);
        
        if (category is null || category.CompanyId != companyId)
            throw new CategoryNotFoundException("Category not found");
        
        await unitOfWork.Categories.DeleteCategoryAsync(id, ct);
        await unitOfWork.CommitAsync(ct);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken ct = default)
    {
        return await unitOfWork.Categories.ExistsCategoryAsync(id, ct);
    }
}