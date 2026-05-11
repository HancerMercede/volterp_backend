using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Domain.Entities;

namespace Volterp.Application.Interfaces;

public interface ISaleService
{
    Task<PagedResult<SaleDto>> GetAllSalesAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<PagedResult<SaleDto>> GetSalesByStatusAsync(int companyId, SaleStatus status, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<SaleDto?> GetSaleByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<SaleDto> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct = default);
    Task<SaleDto> UpdateSaleAsync(int id, int companyId, UpdateSaleRequest request, CancellationToken ct = default);
    Task<SaleDto> CompleteSaleAsync(int id, int companyId, CancellationToken ct = default);
    Task DeleteSaleAsync(int id, int companyId, CancellationToken ct = default);
}