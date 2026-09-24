using EitherWay;
using Volterp.Application.DTOs.SupplierDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class SupplierService(IUnitOfWork unitOfWork) : ISupplierService
{
    public async Task<PagedResult<SupplierDto>> GetAllSuppliersAsync(Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var suppliers = await unitOfWork.Suppliers.GetAllSuppliersByCompanyAsync(companyId, pageNumber, pageSize, ct);

        return suppliers.MapTo<Supplier, SupplierDto>();
    }

    public async Task<Either<Error,SupplierDto?>> GetSupplierByIdAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("The id must not be empty."))
            .Ensure(_ => companyId != Guid.Empty, new Error("The companyId must not be empty."))
            .FlatMap(async (s) => await unitOfWork.Suppliers.GetSupplierByIdAsync(s, companyId, ct),
                exception => new Error($"Error: {exception.Message}"))
            .Map(supplier => supplier?.MapTo<Supplier, SupplierDto>())
            .Run();
    }

    public async Task<Either<Error, SupplierDto>> CreateSupplierAsync(SupplierCreateDto request, Guid companyId, Guid? userId, CancellationToken ct = default)
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
            .Ensure(_=> companyId != Guid.Empty,
                new Error("The companyId must not be empty."))
            .Map(_ => request.Project())
            .Map(supplier =>
            {
                supplier.CompanyId = companyId;
                if (userId.HasValue) supplier.SetCreatedBy(userId.Value);
                return supplier;
            })
            .FlatMap(async supplier => await unitOfWork.Suppliers.AddSupplierAsync(supplier, ct),
                exception => new Error($"Error: {exception.Message}"))
            .Map(supplier => supplier.MapTo<Supplier, SupplierDto>())
            .Run();
    }

    public async Task<Either<Error,SupplierDto>> UpdateSupplierAsync(Guid id, Guid companyId, UpdateSupplierRequest request, Guid? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>.FromRight(id)
            .Ensure(s => s != Guid.Empty, new Error("The id must not be empty."))
            .Ensure(_ => companyId != Guid.Empty, new Error("The companyId must not be empty."))
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

    public async Task<Either<Error, Unit>> DeleteSupplierAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {

        return await EitherAsync<Error, Guid>.FromRight(id)
            .Ensure(s => s != Guid.Empty, new Error("the id must not be empty."))
            .Ensure(_ => companyId != Guid.Empty, new Error("the companyId must not be empty."))
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
