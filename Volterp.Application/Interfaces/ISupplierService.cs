using EitherWay;
using Volterp.Application.DTOs.SupplierDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> GetAllSuppliersAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<Either<Error, SupplierDto?>> GetSupplierByIdAsync(Guid id, CancellationToken ct = default);
    Task<Either<Error,SupplierDto>> CreateSupplierAsync(SupplierCreateDto request, Guid companyId, Guid? userId, CancellationToken ct = default);
    Task<Either<Error,SupplierDto>> UpdateSupplierAsync(Guid id, Guid companyId, UpdateSupplierRequest request, Guid? userId, CancellationToken ct = default);
    Task<Either<Error, Unit>> DeleteSupplierAsync(Guid id, Guid companyId, CancellationToken ct = default);
}
