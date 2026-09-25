using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface ISupplierRepository
{
    Task<PagedResult<Supplier>> GetAllSuppliersAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Supplier?> GetSupplierByIdAsync(Guid id, CancellationToken ct = default);
    Task<Supplier> AddSupplierAsync(Supplier supplier, CancellationToken ct = default);
    Task UpdateSupplierAsync(Supplier supplier, CancellationToken ct = default);
    Task DeleteSupplierAsync(Guid id, CancellationToken ct = default);
}
