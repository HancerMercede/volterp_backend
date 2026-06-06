using Volterp.Application.DTOs;
using Volterp.Application.DTOs.CompanyDtos;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;

namespace Volterp.Application.Services;

public class CompanyService(IUnitOfWork unitOfWork) :ICompanyService
{
    public async Task<PagedResult<CompanyDto>> GetAllCompaniesAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var companies = await unitOfWork.Companies.GetAllCompaniesAsync(pageNumber, pageSize, ct);
        return companies.MapTo<Company, CompanyDto>();
    }

    public async Task<CompanyDto?> GetCompanyByIdAsync(int id, CancellationToken ct = default)
    {
        var companyDto = await unitOfWork.Companies.GetCompanyByIdAsync(id, ct);
        return companyDto?.MapTo<Company, CompanyDto>();
    }

    public async Task<CompanyDto?> AddCompanyAsync(CreateCompanyDto company, CancellationToken ct = default)
    {
        var companyDb = company.Project();
        
        await unitOfWork.Companies.AddCompanyAsync(companyDb, ct);
        await unitOfWork.CommitAsync(ct);
        
        return companyDb.MapTo<Company, CompanyDto>();
    }

    public async Task<CompanyDto?> UpdateCompanyAsync(int companyId, UpdateCompanyDto company, CancellationToken ct = default)
    {
        var companyToUpdate =  await unitOfWork.Companies.GetCompanyByIdAsync(companyId, ct);
       
        companyToUpdate?.Apply(c =>
        {
             c.Name = company.Name;
             c.TaxId = company.TaxId;
             c.LogoUrl = company.LogoUrl;
             c.Address = company.Address;
             c.LegalName = company.LegalName;
             c.Phone = company.Phone;
             c.Email = company.Email;
             c.UpdatedAt = DateTime.UtcNow;
        });
       await unitOfWork.Companies.UpdateCompanyAsync(companyToUpdate!, ct);
       await  unitOfWork.CommitAsync(ct);
       
       return companyToUpdate?.MapTo<Company, CompanyDto>();
    }

    public async Task DeleteCompanyAsync(int id, CancellationToken ct = default) 
        => await  unitOfWork.Companies.DeleteCompanyAsync(id, ct);
    public async Task<bool> ExistsCompanyAsync(int id, CancellationToken ct = default) 
        => await unitOfWork.Companies.ExistsAsync(c=>c.Id == id, ct);
}