using Volterp.Application.Helpers;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.Interfaces;

public interface ISaleRepository : IRepositoryBase<Sale>
{
    Task<Sale?> GetSaleByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);
    Task<PagedResult<Sale>> GetAllSalesByCompanyAsync(Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<PagedResult<Sale>> GetSalesByStatusAsync(Guid companyId, SaleStatus status, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Sale> AddSaleAsync(Sale sale, CancellationToken ct = default);
    Task UpdateSaleAsync(Sale sale, CancellationToken ct = default);
    Task DeleteSaleAsync(Guid id, CancellationToken ct = default);
}
