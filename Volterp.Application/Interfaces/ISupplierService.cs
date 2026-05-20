using Volterp.Application.DTOs;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> GetAllSuppliersAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<SupplierDto?> GetSupplierByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<SupplierDto> CreateSupplierAsync(SupplierDto request, int companyId, CancellationToken ct = default);
    Task<SupplierDto> UpdateSupplierAsync(int id, int companyId, SupplierDto request, CancellationToken ct = default);
    Task DeleteSupplierAsync(int id, int companyId, CancellationToken ct = default);
}