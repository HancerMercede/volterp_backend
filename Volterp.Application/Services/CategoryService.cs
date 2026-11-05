using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.CategoryDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.Services;

public class CategoryService(IUnitOfWork unitOfWork) : ICategoryService
{
    private const string CodeAlreadyInUseMessage = "The code is already in use.";

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
                // The user may bring its own code; otherwise the per-company sequence
                // provides one. A user-provided code must be free inside the company.
                category.Code = await ResolveCodeForCreateAsync(request.Code, companyId, ct);

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
            .FlatMap(async category =>
            {
                // An empty code keeps the current one; a new code must be free
                // inside the company (the row being updated is excluded).
                var resolvedCode = await ResolveCodeForUpdateAsync(request.Code, category.Code, companyId, category.Id, ct);

                category.Apply(x =>
                {
                    x.Name = request.Name;
                    x.Description = request.Description;
                    x.IsActive = request.IsActive;
                    x.Code = resolvedCode;
                    x.UpdatedAt = DateTime.UtcNow;
                    if (userId.HasValue) x.SetUpdatedBy(userId.Value);
                });

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

    /// <summary>
    /// Resolves the code of a new category: the user-provided one (trimmed) when it
    /// comes with a value, otherwise the next code of the company sequence.
    /// </summary>
    private async Task<string> ResolveCodeForCreateAsync(string? requestedCode, Guid companyId, CancellationToken ct)
    {
        var trimmed = requestedCode?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return await unitOfWork.Codes.NextAsync(companyId, CodeEntityType.Category, ct);

        if (await unitOfWork.Categories.ExistsCodeAsync(companyId, trimmed, null, ct))
            throw new Exception(CodeAlreadyInUseMessage);

        return trimmed;
    }

    /// <summary>
    /// Resolves the code of an existing category: the user-provided one (trimmed) when it
    /// comes with a value, otherwise the code the category already has.
    /// </summary>
    private async Task<string> ResolveCodeForUpdateAsync(string? requestedCode, string currentCode, Guid companyId, Guid id, CancellationToken ct)
    {
        var trimmed = requestedCode?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return currentCode;

        if (await unitOfWork.Categories.ExistsCodeAsync(companyId, trimmed, id, ct))
            throw new Exception(CodeAlreadyInUseMessage);

        return trimmed;
    }
}
