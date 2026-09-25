using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ProductDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, ProductDto?>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Either<Error, ProductDto>> CreateAsync(CreateProductDto request, Guid companyId, Guid? userId, CancellationToken ct = default);
    Task<Either<Error, ProductDto>> UpdateAsync(Guid id, UpdateProductDto request, Guid companyId, Guid? userId, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeleteAsync(Guid id, Guid companyId, CancellationToken ct = default);
}
