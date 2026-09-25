using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface IProductRepository : IRepositoryBase<Product>
{
    Task<Product?> GetProductByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<Product>> GetAllProductsAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Product> AddProductAsync(Product product, CancellationToken ct = default);
    Task UpdateProductAsync(Product product, CancellationToken ct = default);
    Task<List<Product>> GetProductsByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task DeleteProductAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsProductAsync(Guid id, CancellationToken ct = default);
}
