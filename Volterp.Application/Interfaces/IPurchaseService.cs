using Volterp.Application.DTOs;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IPurchaseService
{
    Task<PagedResult<PurchaseDto>> GetAllPurchasesAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<PurchaseDto?> GetPurchaseByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<PurchaseDto> CreatePurchaseAsync(PurchaseDto request, int companyId, int? userId, CancellationToken ct = default);
    Task<PurchaseDto> UpdatePurchaseAsync(int id, int companyId, PurchaseDto request, int? userId, CancellationToken ct = default);
    Task DeletePurchaseAsync(int id, int companyId, CancellationToken ct = default);
}