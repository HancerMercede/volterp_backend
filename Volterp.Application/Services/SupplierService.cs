using EitherWay;
using Volterp.Application.DTOs.SupplierDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.Services;

public class SupplierService(IUnitOfWork unitOfWork) : ISupplierService
{
    private const string CodeAlreadyInUseMessage = "The code is already in use.";

    public async Task<PagedResult<SupplierDto>> GetAllSuppliersAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var suppliers = await unitOfWork.Suppliers.GetAllSuppliersAsync(pageNumber, pageSize, ct);

        return suppliers.MapTo<Supplier, SupplierDto>();
    }

    public async Task<Either<Error,SupplierDto?>> GetSupplierByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("The id must not be empty."))
            .FlatMap(async (s) => await unitOfWork.Suppliers.GetSupplierByIdAsync(s, ct),
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
            .Map(_ => request.Project())
            .Map(supplier =>
            {
                supplier.CompanyId = companyId;
                if (userId.HasValue) supplier.SetCreatedBy(userId.Value);
                return supplier;
            })
            .FlatMap(async supplier =>
            {
                // The user may bring its own code; otherwise the per-company sequence
                // provides one. A user-provided code must be free inside the company.
                supplier.Code = await ResolveCodeForCreateAsync(request.Code, companyId, ct);

                return supplier;
            }, exception => new Error(exception.Message))
            .FlatMap(async supplier =>
            {
                await unitOfWork.Suppliers.AddSupplierAsync(supplier, ct);
                await unitOfWork.CommitAsync(ct);
                return supplier;
            }, exception => new Error($"Error: {exception.Message}"))
            .Map(supplier => supplier.MapTo<Supplier, SupplierDto>())
            .Run();
    }

    public async Task<Either<Error, SupplierDto>> UpdateSupplierAsync(Guid id, Guid companyId, UpdateSupplierRequest request, Guid? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(s => s != Guid.Empty, new Error("The id must not be empty."))
            .Ensure(_ => !string.IsNullOrEmpty(request.Name), new Error("The name must be specified."))
            .Ensure(_ => !string.IsNullOrEmpty(request.Address), new Error("The address must be specified."))
            .Ensure(_ => !string.IsNullOrEmpty(request.Email), new Error("The email must be specified."))
            .Ensure(_=> request.Email.IsValidEmail(), _=> new Error("The email is not valid"))
            .FlatMap(async supplier => await unitOfWork.Suppliers.GetSupplierByIdAsync(id, ct),
                exception => new Error($"Error: {exception.Message}"))
            .FlatMap(async supplier =>
            {
                if (supplier is null)
                    return supplier;

                // An empty code keeps the current one; a new code must be free
                // inside the company (the row being updated is excluded).
                var resolvedCode = await ResolveCodeForUpdateAsync(request.Code, supplier.Code, companyId, supplier.Id, ct);

                supplier.Apply(s =>
                {
                    s.Address = request.Address!;
                    s.Category = request.Category!;
                    s.Name = request.Name!;
                    s.Email = request.Email!;
                    s.ImageUrl = request.ImageUrl;
                    s.ContactPerson = request.ContactPerson!;
                    s.Code = resolvedCode;
                    s.UpdatedAt = DateTime.Now;
                    if (userId.HasValue) s.SetUpdatedBy(userId.Value);

                });

                return supplier;
            }, exception => new Error(exception.Message))
            .FlatMap(async supplier =>
                {
                  await unitOfWork.Suppliers.UpdateSupplierAsync(supplier!, ct);
                  await unitOfWork.CommitAsync(ct);
                  return supplier;
                },
                exception => new Error($"Error: {exception.Message}"))
            .Map(supplier => supplier!.MapTo<Supplier, SupplierDto>())
            .Run();
    }

    public async Task<Either<Error, Unit>> DeleteSupplierAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {

        return await EitherAsync<Error, Guid>.FromRight(id)
            .Ensure(s => s != Guid.Empty, new Error("the id must not be empty."))
            .FlatMap(async supplierId =>
            {
                var supplier = await unitOfWork.Suppliers.GetSupplierByIdAsync(supplierId, ct);
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

    /// <summary>
    /// Resolves the code of a new supplier: the user-provided one (trimmed) when it
    /// comes with a value, otherwise the next code of the company sequence.
    /// </summary>
    private async Task<string> ResolveCodeForCreateAsync(string? requestedCode, Guid companyId, CancellationToken ct)
    {
        var trimmed = requestedCode?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return await unitOfWork.Codes.NextAsync(companyId, CodeEntityType.Supplier, ct);

        if (await unitOfWork.Suppliers.ExistsCodeAsync(companyId, trimmed, null, ct))
            throw new Exception(CodeAlreadyInUseMessage);

        return trimmed;
    }

    /// <summary>
    /// Resolves the code of an existing supplier: the user-provided one (trimmed) when it
    /// comes with a value, otherwise the code the supplier already has.
    /// </summary>
    private async Task<string> ResolveCodeForUpdateAsync(string? requestedCode, string currentCode, Guid companyId,
        Guid id, CancellationToken ct)
    {
        var trimmed = requestedCode?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return currentCode;

        if (await unitOfWork.Suppliers.ExistsCodeAsync(companyId, trimmed, id, ct))
            throw new Exception(CodeAlreadyInUseMessage);

        return trimmed;
    }
}
