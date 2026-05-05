using Microsoft.EntityFrameworkCore;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class ProductRepository(VolterpDbContext context)
    : RepositoryBase<Product>(context), IProductRepository
{
    public async Task<Product?> GetProductByIdAsync(int id, CancellationToken ct = default)
        => await GetByCondictionsAsync(p => p.Id == id, ct);

    public async Task<List<Product>> GetAllProductsByCompanyAsync(int companyId, CancellationToken ct = default)
        => await GetAllAsync(p => p.CompanyId == companyId, ct);

   

    public async Task<Product> AddProductAsync(Product product, CancellationToken ct = default)
    {
        await AddAsync(product, ct);
        return product;
    }

    public async Task UpdateProductAsync(Product product, CancellationToken ct = default)
        => await UpdateAsync(product, ct);

    public async Task DeleteProductAsync(int id, CancellationToken ct = default)
    {
        var product = await GetByIdAsync(id, ct);
        if (product is not null)
            await DeleteAsync(product, ct);
    }

    public async Task<bool> ExistsProductAsync(int id, CancellationToken ct = default)
        => await ExistsAsync(p => p.Id == id, ct);
}