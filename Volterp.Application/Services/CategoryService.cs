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
    public async Task<PagedResult<CategoryDto>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var categories = await unitOfWork.Categories.GetAllCategoriesAsync(pageNumber, pageSize, ct);
        return categories.MapTo<Category, CategoryDto>();
    }

    public async Task<Either<Error, CategoryDto?>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async categoryId => await unitOfWork.Categories.GetCategoryByIdAsync(categoryId, ct),
                ex => new Error(ex.Message))
            .Map(category => category is null ? null : category.MapTo<Category, CategoryDto>())
            .Run();
    }

    public async Task<Either<Error, CategoryDto>> CreateAsync(CreateCategoryRequest request, Guid companyId, Guid? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, CreateCategoryRequest>
            .FromRight(request)
            .Ensure(r => !string.IsNullOrWhiteSpace(r.Name), new Error("The name is required."))
            .Map(r => r.Project())
            .Map(category =>
            {
                category.CompanyId = companyId;
                if (userId.HasValue) category.SetCreatedBy(userId.Value);
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

    public async Task<Either<Error, CategoryDto>> UpdateAsync(Guid id, UpdateCategoryRequest request, Guid companyId, Guid? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async categoryId => await unitOfWork.Categories.GetCategoryByIdAsync(categoryId, ct),
                ex => new Error(ex.Message))
            .Ensure(category => category is not null,
                new Error("Category not found."))
            .Map(category =>
            {
                category.Apply(x =>
                {
                    x.Name = request.Name;
                    x.Description = request.Description;
                    x.IsActive = request.IsActive;
                    x.UpdatedAt = DateTime.UtcNow;
                    if (userId.HasValue) x.SetUpdatedBy(userId.Value);
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

    public async Task<Either<Error, Unit>> DeleteAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async categoryId => await unitOfWork.Categories.GetCategoryByIdAsync(categoryId, ct),
                ex => new Error(ex.Message))
            .Ensure(category => category is not null,
                new Error("Category not found."))
            .FlatMap(async category =>
            {
                await unitOfWork.Categories.DeleteCategoryAsync(category.Id, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, ex => new Error(ex.Message))
            .Run();
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await unitOfWork.Categories.ExistsCategoryAsync(id, ct);
    }
}
