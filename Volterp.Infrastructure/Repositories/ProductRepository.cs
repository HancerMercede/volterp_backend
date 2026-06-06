using Microsoft.EntityFrameworkCore;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class ProductRepository(VolterpDbContext context)
    : RepositoryBase<Product>(context), IProductRepository
{
    public async Task<Product?> GetProductByIdAsync(int id, CancellationToken ct = default)
        => await GetByCondictionsAsync(p => p.Id == id, ct);

    public async Task<PagedResult<Product>> GetAllProductsByCompanyAsync(int companyId,int pageNumber, int pageSize, CancellationToken ct = default)
    {
        return await GetAllAsync(p => p.CompanyId == companyId, pageNumber, pageSize, ct);
    }
    
    public async Task<Product> AddProductAsync(Product product, CancellationToken ct = default)
    {
        await AddAsync(product, ct);
        return product;
    }

    public async Task UpdateProductAsync(Product product, CancellationToken ct = default)
        => await UpdateAsync(product, ct);

    public async Task<List<Product>> GetProductsByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
        => await Set().Where(p => ids.Contains(p.Id)).ToListAsync(ct);

    public async Task DeleteProductAsync(int id, CancellationToken ct = default)
    {
        var product = await GetByIdAsync(id, ct);
        if (product is not null)
            await DeleteAsync(product, ct);
    }

    public async Task<bool> ExistsProductAsync(int id, CancellationToken ct = default)
        => await ExistsAsync(p => p.Id == id, ct);
}