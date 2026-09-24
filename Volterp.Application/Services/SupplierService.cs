using EitherWay;
using Volterp.Application.DTOs.SupplierDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class SupplierService(IUnitOfWork unitOfWork) : ISupplierService
{
    public async Task<PagedResult<SupplierDto>> GetAllSuppliersAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var suppliers = await unitOfWork.Suppliers.GetAllSuppliersByCompanyAsync(companyId, pageNumber, pageSize, ct);

        return suppliers.MapTo<Supplier, SupplierDto>();
    }

    public async Task<Either<Error,SupplierDto?>> GetSupplierByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("The id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("The companyId must be greater than zero."))
            .FlatMap(async (s) => await unitOfWork.Suppliers.GetSupplierByIdAsync(s, companyId, ct),
                exception => new Error($"Error: {exception.Message}"))
            .Map(supplier => supplier?.MapTo<Supplier, SupplierDto>())
            .Run();
    }

    public async Task<Either<Error, SupplierDto>> CreateSupplierAsync(SupplierCreateDto request, int companyId, int? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, SupplierCreateDto>
            .FromRight(request)
            .Ensure(x => !string.IsNullOrEmpty(x.Name),
                new Error("The name must be specified."))
            .Ensure(x => !string.IsNullOrEmpty(x.Address),
                new Error("The address must be specified."))
            .Ensure(x => !string.IsNullOrEmpty(x.Email),
                new Error("The email must be specified."))
            .Ensure(_=> request.Email.IsValidEmail(), _=> new Error("The email is not valid"))
            .Ensure(_=> companyId > 0,
                new Error("The companyId must be greater than zero."))
            .Map(_ => request.Project())
            .Map(supplier =>
            {
                // Cycle 1.1 fix (bug #2): SupplierCreateDto.MapTo() no longer copies
                // CompanyId from the request body (it doesn't exist anymore). The
                // service assigns the JWT-derived companyId parameter here. Any
                // attempt to set CompanyId before this point is ignored.
                supplier.CompanyId = companyId;
                if (userId.HasValue) supplier.SetCreatedBy(userId.Value);
                return supplier;
            })
            .FlatMap(async supplier => await unitOfWork.Suppliers.AddSupplierAsync(supplier, ct),
                exception => new Error($"Error: {exception.Message}"))
            .Map(supplier => supplier.MapTo<Supplier, SupplierDto>())
            .Run();
    }
    
    public async Task<Either<Error,SupplierDto>> UpdateSupplierAsync(int id, int companyId, UpdateSupplierRequest request, int? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>.FromRight(id)
            .Ensure(s => s > 0, new Error("The id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("The companyId must be greater than zero."))
            .Ensure(_ => !string.IsNullOrEmpty(request.Name), new Error("The name must be specified."))
            .Ensure(_ => !string.IsNullOrEmpty(request.Address), new Error("The address must be specified."))
            .Ensure(_ => !string.IsNullOrEmpty(request.Email), new Error("The email must be specified."))
            .Ensure(_=> request.Email.IsValidEmail(), _=> new Error("The email is not valid"))
            .FlatMap(async supplier => await unitOfWork.Suppliers.GetSupplierByIdAsync(id, companyId, ct),
                exception => new Error($"Error: {exception.Message}"))
            .Map(supplier =>
            {
               return supplier.Apply(s =>
                {
                    s.Address = request.Address!;
                    s.Category = request.Category!;
                    s.Name = request.Name!;
                    s.Email = request.Email!;
                    // Cycle 1.1 fix (bug #3): the existing tenant is preserved by
                    // construction. UpdateSupplierRequest no longer carries CompanyId
                    // so there is no inbound value to copy. The GetSupplierByIdAsync
                    // call above already enforces tenant scope (returns null for
                    // wrong tenants), so 's.CompanyId' is the trusted tenant.
                    s.ImageUrl = request.ImageUrl;
                    s.ContactPerson = request.ContactPerson!;
                    s.UpdatedAt = DateTime.Now;
                    if (userId.HasValue) s.SetUpdatedBy(userId.Value);

                });
            }).FlatMap(async supplier =>
                {
                  await unitOfWork.Suppliers.UpdateSupplierAsync(supplier!, ct);
                  return supplier;
                },
                exception => new Error($"Error: {exception.Message}"))
            .Map(s => s?.MapTo<Supplier, SupplierDto>())
            .Run();
    }
    
    public async Task<Either<Error, Unit>> DeleteSupplierAsync(int id, int companyId, CancellationToken ct = default)
    {

        return await EitherAsync<Error, int>.FromRight(id)
            .Ensure(s => s > 0, new Error("the id must be greater than zero."))
            .Ensure(_ => companyId > 0, new Error("the companyId must be greater than zero."))
            .FlatMap(async supplierId =>
            {
                var supplier = await unitOfWork.Suppliers.GetSupplierByIdAsync(supplierId, companyId, ct);
                return supplier;
            }, exception => new Error($"error:{exception.Message}"))
            .FlatMap(async supplier =>
            {
                await unitOfWork.Suppliers.DeleteSupplierAsync(supplier.Id, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, exception => new Error($"Error:{exception.Message}"))
            .Run();
    }
}