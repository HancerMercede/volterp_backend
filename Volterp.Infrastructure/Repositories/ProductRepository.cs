using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class ProductRepository(VolterpDbContext context)
    : RepositoryBase<Product>(context), IProductRepository
{
    public async Task<Product?> GetProductByIdAsync(Guid id, CancellationToken ct = default)
        => await GetByCondictionsAsync(p => p.Id == id, ct);

    public async Task<PagedResult<Product>> GetAllProductsAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        return await GetAllAsync(_ => true, pageNumber, pageSize, ct);
    }

    public async Task<Product> AddProductAsync(Product product, CancellationToken ct = default)
    {
        await AddAsync(product, ct);
        return product;
    }

    public async Task UpdateProductAsync(Product product, CancellationToken ct = default)
        => await UpdateAsync(product, ct);

    public async Task<List<Product>> GetProductsByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        => await Set().Where(p => ids.Contains(p.Id)).ToListAsync(ct);

    public async Task DeleteProductAsync(Guid id, CancellationToken ct = default)
    {
        var product = await GetByIdAsync(id, ct);
        if (product is not null)
            await DeleteAsync(product, ct);
    }

    public async Task<bool> ExistsProductAsync(Guid id, CancellationToken ct = default)
        => await ExistsAsync(p => p.Id == id, ct);
}
