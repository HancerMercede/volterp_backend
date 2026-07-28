using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IPurchaseService
{
    Task<PagedResult<PurchaseDto>> GetAllPurchasesAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, PurchaseDto?>> GetPurchaseByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<Either<Error, PurchaseDto>> CreatePurchaseAsync(PurchaseDto request, int companyId, int? userId, CancellationToken ct = default);
    Task<Either<Error, PurchaseDto>> UpdatePurchaseAsync(int id, int companyId, PurchaseDto request, int? userId, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeletePurchaseAsync(int id, int companyId, CancellationToken ct = default);
}