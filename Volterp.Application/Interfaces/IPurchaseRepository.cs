using System.Linq.Expressions;
using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface  IPurchaseRepository
{
    Task<PagedResult<Purchase>> GetAllPurchasesAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Purchase?> GetPurchaseByIdAsync(Guid id, CancellationToken ct = default);
    Task<Purchase> AddPurchaseAsync(Purchase purchase, CancellationToken ct = default);
    Task UpdatePurchaseAsync(Purchase purchase, CancellationToken ct = default);
    Task DeletePurchaseAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsCodeAsync(Guid companyId, string code, Guid? excludeId = null, CancellationToken ct = default);
}
