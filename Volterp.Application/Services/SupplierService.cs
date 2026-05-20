using Volterp.Application.DTOs;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class SupplierService(IUnitOfWork unitOfWork) : ISupplierService
{
    public async Task<PagedResult<SupplierDto>> GetAllSuppliersAsync(int companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var suppliers = await unitOfWork.Suppliers.GetAllSuppliersByCompanyAsync(companyId, pageNumber, pageSize, ct);
        
        return suppliers.Map(s => new SupplierDto(
            s.Id, s.Name, s.Email, s.Phone, s.Address, s.Category,
            s.ContactPerson, s.IsActive, s.CreatedAt
        ));
    }

    public async Task<SupplierDto?> GetSupplierByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        var supplier = await unitOfWork.Suppliers.GetSupplierByIdAsync(id, companyId, ct);
        
        if (supplier is null) return null;
        
        return supplier.Map(s => new SupplierDto(
            s.Id, s.Name, s.Email, s.Phone, s.Address, s.Category,
            s.ContactPerson, s.IsActive, s.CreatedAt
        ));
    }

    public async Task<SupplierDto> CreateSupplierAsync(CreateSupplierRequest request, int companyId, CancellationToken ct = default)
    {
        var supplier = request.Map(r => new Supplier
        {
            CompanyId = companyId,
            Name = r.Name,
            Email = r.Email,
            Phone = r.Phone,
            Address = r.Address,
            Category = r.Category,
            ContactPerson = r.ContactPerson,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await unitOfWork.Suppliers.AddSupplierAsync(supplier, ct);
        await unitOfWork.CommitAsync(ct);

        return supplier.Map(s => new SupplierDto(
            s.Id, s.Name, s.Email, s.Phone, s.Address, s.Category,
            s.ContactPerson, s.IsActive, s.CreatedAt
        ));
    }

    public async Task<SupplierDto> UpdateSupplierAsync(int id, int companyId, UpdateSupplierRequest request, CancellationToken ct = default)
    {
        var supplier = await unitOfWork.Suppliers.GetSupplierByIdAsync(id, companyId, ct);
        
        if (supplier is null)
            throw new ArgumentException("Supplier not found");

        supplier.Apply(s =>
        {
            s.Name = request.Name;
            s.Email = request.Email;
            s.Phone = request.Phone;
            s.Address = request.Address;
            s.Category = request.Category;
            s.ContactPerson = request.ContactPerson;
            s.IsActive = request.IsActive;
            s.UpdatedAt = DateTime.UtcNow;
        });

        await unitOfWork.Suppliers.UpdateSupplierAsync(supplier, ct);
        await unitOfWork.CommitAsync(ct);

        return supplier.Map(s => new SupplierDto(
            s.Id, s.Name, s.Email, s.Phone, s.Address, s.Category,
            s.ContactPerson, s.IsActive, s.CreatedAt
        ));
    }

    public async Task DeleteSupplierAsync(int id, int companyId, CancellationToken ct = default)
    {
        var supplier = await unitOfWork.Suppliers.GetSupplierByIdAsync(id, companyId, ct);
        
        if (supplier is null)
            throw new ArgumentException("Supplier not found");

        await unitOfWork.Suppliers.DeleteSupplierAsync(id, ct);
        await unitOfWork.CommitAsync(ct);
    }
}