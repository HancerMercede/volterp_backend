using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface ISaleRepository : IRepositoryBase<Sale>
{
    Task<Sale?> GetSaleByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<PagedResult<Sale>> GetAllSalesByCompanyAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<PagedResult<Sale>> GetSalesByStatusAsync(int companyId, SaleStatus status, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Sale> AddSaleAsync(Sale sale, CancellationToken ct = default);
    Task UpdateSaleAsync(Sale sale, CancellationToken ct = default);
    Task DeleteSaleAsync(int id, CancellationToken ct = default);
}