using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface  IPurchaseRepository
{
    Task<PagedResult<Purchase>> GetAllPurchasesByCompanyAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Purchase?> GetPurchaseByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<Purchase> AddPurchaseAsync(Purchase purchase, CancellationToken ct = default);
    Task UpdatePurchaseAsync(Purchase purchase, CancellationToken ct = default);
    Task DeletePurchaseAsync(int id, CancellationToken ct = default);
}