using Volterp.Application.DTOs;
using Volterp.Application.DTOs.ProductDtos;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetAllAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<ProductDto?> GetByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductDto request, int companyId, CancellationToken ct = default);
    Task<ProductDto> UpdateAsync(int id, UpdateProductDto request, int companyId, CancellationToken ct = default);
    Task DeleteAsync(int id, int companyId, CancellationToken ct = default);
}