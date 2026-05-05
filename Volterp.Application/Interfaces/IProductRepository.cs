using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface IProductRepository : IRepositoryBase<Product>
{
    Task<Product?> GetProductByIdAsync(int id, CancellationToken ct = default);
    Task<List<Product>> GetAllProductsByCompanyAsync(int companyId, CancellationToken ct = default);
    Task<Product> AddProductAsync(Product product, CancellationToken ct = default);
    Task UpdateProductAsync(Product product, CancellationToken ct = default);
    Task DeleteProductAsync(int id, CancellationToken ct = default);
    Task<bool> ExistsProductAsync(int id, CancellationToken ct = default);
}