using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface ISupplierRepository : IRepositoryBase<Supplier>
{
    Task<Supplier?> GetSupplierByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<PagedResult<Supplier>> GetAllSuppliersByCompanyAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Supplier> AddSupplierAsync(Supplier supplier, CancellationToken ct = default);
    Task UpdateSupplierAsync(Supplier supplier, CancellationToken ct = default);
    Task DeleteSupplierAsync(int id, CancellationToken ct = default);
}