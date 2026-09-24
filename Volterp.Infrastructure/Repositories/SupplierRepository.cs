using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class SupplierRepository(VolterpDbContext context) : RepositoryBase<Supplier>(context), ISupplierRepository
{

    public async Task<PagedResult<Supplier>> GetAllSuppliersByCompanyAsync(Guid companyId, int pageNumber, int pageSize,
        CancellationToken ct = default)
        => await GetAllAsync(_ => true, pageNumber, pageSize, ct);

    public async Task<Supplier?> GetSupplierByIdAsync(Guid id, Guid companyId, CancellationToken ct = default)
        => await GetByCondictionsAsync(s => s.Id == id, ct);

    public async Task<Supplier> AddSupplierAsync(Supplier supplier, CancellationToken ct = default)
    {
        await AddAsync(supplier, ct);
        return supplier;
    }

    public async Task UpdateSupplierAsync(Supplier supplier, CancellationToken ct = default)
        => await UpdateAsync(supplier, ct);

    public async Task DeleteSupplierAsync(Guid id, CancellationToken ct = default)
    {
        var supplier = await GetByIdAsync(id, ct);
        if (supplier is not null)
            await DeleteAsync(supplier, ct);
    }
}
