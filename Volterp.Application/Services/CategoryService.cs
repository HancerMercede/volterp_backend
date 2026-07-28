using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.CategoryDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class CategoryService(IUnitOfWork unitOfWork) : ICategoryService
{
    public async Task<PagedResult<CategoryDto>> GetAllAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var categories = await unitOfWork.Categories.GetAllCategoriesByCompanyAsync(companyId, pageNumber, pageSize, ct);
        return categories.MapTo<Category, CategoryDto>();
    }

    public async Task<Either<Error, CategoryDto?>> GetByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async categoryId => await unitOfWork.Categories.GetCategoryByIdAsync(categoryId, ct),
                ex => new Error(ex.Message))
            .Map(category => category is null || category.CompanyId != companyId ? null : category.MapTo<Category, CategoryDto>())
            .Run();
    }

    public async Task<Either<Error, CategoryDto>> CreateAsync(CreateCategoryRequest request, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, CreateCategoryRequest>
            .FromRight(request)
            .Ensure(r => !string.IsNullOrWhiteSpace(r.Name), new Error("The name is required."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .Map(r => r.Project())
            .Map(category =>
            {
                category.CompanyId = companyId;
                return category;
            })
            .FlatMap(async category =>
            {
                await unitOfWork.Categories.AddCategoryAsync(category, ct);
                await unitOfWork.CommitAsync(ct);
                return category;
            }, ex => new Error(ex.Message))
            .Map(category => category.MapTo<Category, CategoryDto>())
            .Run();
    }

    public async Task<Either<Error, CategoryDto>> UpdateAsync(int id, UpdateCategoryRequest request, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async categoryId => await unitOfWork.Categories.GetCategoryByIdAsync(categoryId, ct),
                ex => new Error(ex.Message))
            .Ensure(category => category is not null && category.CompanyId == companyId,
                new Error("Category not found."))
            .Map(category =>
            {
                category.Apply(x =>
                {
                    x.Name = request.Name;
                    x.Description = request.Description;
                    x.IsActive = request.IsActive;
                    x.UpdatedAt = DateTime.UtcNow;
                });
                return category;
            })
            .FlatMap(async category =>
            {
                await unitOfWork.Categories.UpdateCategoryAsync(category, ct);
                await unitOfWork.CommitAsync(ct);
                return category;
            }, ex => new Error(ex.Message))
            .Map(category => category.MapTo<Category, CategoryDto>())
            .Run();
    }

    public async Task<Either<Error, Unit>> DeleteAsync(int id, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async categoryId => await unitOfWork.Categories.GetCategoryByIdAsync(categoryId, ct),
                ex => new Error(ex.Message))
            .Ensure(category => category is not null && category.CompanyId == companyId,
                new Error("Category not found."))
            .FlatMap(async category =>
            {
                await unitOfWork.Categories.DeleteCategoryAsync(category.Id, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, ex => new Error(ex.Message))
            .Run();
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken ct = default)
    {
        return await unitOfWork.Categories.ExistsCategoryAsync(id, ct);
    }
}