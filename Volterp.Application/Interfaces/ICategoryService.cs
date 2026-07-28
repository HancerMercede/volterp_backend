using EitherWay;
using Volterp.Application.DTOs.CategoryDtos;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

public interface ICategoryService
{
    Task<PagedResult<CategoryDto>> GetAllAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, CategoryDto?>> GetByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<Either<Error, CategoryDto>> CreateAsync(CreateCategoryRequest request, int companyId, CancellationToken ct = default);
    Task<Either<Error, CategoryDto>> UpdateAsync(int id, UpdateCategoryRequest request, int companyId, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeleteAsync(int id, int companyId, CancellationToken ct = default);
    Task<bool> ExistsAsync(int id, CancellationToken ct = default);
}