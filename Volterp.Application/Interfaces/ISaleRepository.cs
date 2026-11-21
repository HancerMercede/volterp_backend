using Volterp.Application.Helpers;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.Interfaces;

public interface ISaleRepository : IRepositoryBase<Sale>
{
    Task<Sale?> GetSaleByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<Sale>> GetAllSalesAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<PagedResult<Sale>> GetSalesByStatusAsync(SaleStatus status, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Sale> AddSaleAsync(Sale sale, CancellationToken ct = default);
    Task UpdateSaleAsync(Sale sale, CancellationToken ct = default);
    Task DeleteSaleAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsCodeAsync(Guid companyId, string code, Guid? excludeId = null, CancellationToken ct = default);

    // The pending sales that nobody decided: the sweep asks for them by
    // the moment they were created.
    Task<IReadOnlyList<Sale>> GetPendingOlderThanAsync(DateTime cutoff, CancellationToken ct = default);

    // The products that the pending sales reserved.
    Task<IReadOnlyList<Product>> GetReservedProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct = default);
}
