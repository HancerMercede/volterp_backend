using EitherWay;
using Volterp.Application.DTOs.SaleDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Domain.Enums;

namespace Volterp.Application.Interfaces;

public interface ISaleService
{
    Task<PagedResult<SaleDto>> GetAllSalesAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<PagedResult<SaleDto>> GetSalesByStatusAsync(SaleStatus status, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, SaleDto?>> GetSaleByIdAsync(Guid id, CancellationToken ct = default);
    Task<Either<Error, SaleDto>> CreateSaleAsync(CreateSaleRequest request, Guid companyId, Guid? userId, CancellationToken ct = default);
    Task<Either<Error, SaleDto>> UpdateSaleAsync(Guid id, Guid companyId, UpdateSaleRequest request, Guid? userId, CancellationToken ct = default);
    Task<Either<Error, SaleDto>> CompleteSaleAsync(Guid id, Guid companyId, Guid? userId, bool asCommercialInvoice = false, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeleteSaleAsync(Guid id, Guid companyId, CancellationToken ct = default);
}
