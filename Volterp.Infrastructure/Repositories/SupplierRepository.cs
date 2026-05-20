using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public class SupplierRepository(VolterpDbContext context) : RepositoryBase<Supplier>(context), ISupplierRepository
{
    public async Task<Supplier?> GetSupplierByIdAsync(int id, int companyId, CancellationToken ct = default)
        => await GetByCondictionsAsync(s => s.Id == id && s.CompanyId == companyId, ct);

    public async Task<PagedResult<Supplier>> GetAllSuppliersByCompanyAsync(int companyId, int pageNumber, int pageSize,
        CancellationToken ct = default)
        => await GetAllAsync(s => s.CompanyId == companyId, pageNumber, pageSize, ct);

    public async Task<Supplier> AddSupplierAsync(Supplier supplier, CancellationToken ct = default)
    {
        await AddAsync(supplier, ct);
        return supplier;
    }

    public async Task UpdateSupplierAsync(Supplier supplier, CancellationToken ct = default)
        => await UpdateAsync(supplier, ct);

    public async Task DeleteSupplierAsync(int id, CancellationToken ct = default)
    {
        var supplier = await GetByIdAsync(id, ct);
        if (supplier is not null)
            await DeleteAsync(supplier, ct);
    }
}