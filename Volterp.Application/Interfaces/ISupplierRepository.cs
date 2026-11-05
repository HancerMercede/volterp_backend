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

    /// <summary>
    /// Tells whether another supplier of the same company already uses <paramref name="code"/>.
    /// <paramref name="excludeId"/> skips a row by id so an entity can keep its own code on update.
    /// </summary>
    Task<bool> ExistsCodeAsync(Guid companyId, string code, Guid? excludeId = null, CancellationToken ct = default);
}
