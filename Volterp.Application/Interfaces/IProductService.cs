using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ProductDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetAllAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, ProductDto?>> GetByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<Either<Error, ProductDto>> CreateAsync(CreateProductDto request, int companyId, CancellationToken ct = default);
    Task<Either<Error, ProductDto>> UpdateAsync(int id, UpdateProductDto request, int companyId, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeleteAsync(int id, int companyId, CancellationToken ct = default);
}