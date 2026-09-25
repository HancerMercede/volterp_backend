using EitherWay;
using Volterp.Application.DTOs.CategoryDtos;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

public interface ICategoryService
{
    Task<PagedResult<CategoryDto>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, CategoryDto?>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Either<Error, CategoryDto>> CreateAsync(CreateCategoryRequest request, Guid companyId, Guid? userId, CancellationToken ct = default);
    Task<Either<Error, CategoryDto>> UpdateAsync(Guid id, UpdateCategoryRequest request, Guid companyId, Guid? userId, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeleteAsync(Guid id, Guid companyId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
}
