using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IPurchaseService
{
    Task<PagedResult<PurchaseDto>> GetAllPurchasesAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, PurchaseDto?>> GetPurchaseByIdAsync(Guid id, CancellationToken ct = default);
    Task<Either<Error, PurchaseDto>> CreatePurchaseAsync(PurchaseDto request, Guid companyId, Guid? userId, CancellationToken ct = default);
    Task<Either<Error, PurchaseDto>> UpdatePurchaseAsync(Guid id, Guid companyId, PurchaseDto request, Guid? userId, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeletePurchaseAsync(Guid id, Guid companyId, CancellationToken ct = default);
}
