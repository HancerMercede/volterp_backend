using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class SupplierRepository(VolterpDbContext context) : RepositoryBase<Supplier>(context), ISupplierRepository
{

    public async Task<PagedResult<Supplier>> GetAllSuppliersAsync(int pageNumber, int pageSize,
        CancellationToken ct = default)
        => await GetAllAsync(_ => true, pageNumber, pageSize, ct);

    public async Task<Supplier?> GetSupplierByIdAsync(Guid id, CancellationToken ct = default)
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

    public async Task<bool> ExistsCodeAsync(Guid companyId, string code, Guid? excludeId = null,
        CancellationToken ct = default)
    {
        if (excludeId.HasValue)
            return await ExistsAsync(s => s.CompanyId == companyId && s.Code == code && s.Id != excludeId.Value, ct);

        return await ExistsAsync(s => s.CompanyId == companyId && s.Code == code, ct);
    }
}
