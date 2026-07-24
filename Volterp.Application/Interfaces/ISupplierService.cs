using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> GetAllSuppliersAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, SupplierDto?>> GetSupplierByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<Either<Error,SupplierDto>> CreateSupplierAsync(SupplierCreateDto request, int companyId, CancellationToken ct = default);
    Task<Either<Error,SupplierDto>> UpdateSupplierAsync(int id, int companyId, SupplierDto request, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeleteSupplierAsync(int id, int companyId, CancellationToken ct = default);
}