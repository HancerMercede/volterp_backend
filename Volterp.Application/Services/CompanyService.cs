using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.CompanyDtos;
using Volterp.Application.Exceptions.AppErrors;
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

    public async Task<Either<Error,CompanyDto?>> GetCompanyByIdAsync(int id, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(id)
            .Ensure(x => x > 0, new Error("id must be greater than zero"))
            .FlatMap(async company => await unitOfWork.Companies.GetCompanyByIdAsync(id, ct),
                error => new Error(error.Message))
            .Ensure(company => company is not null, new Error("company not found"))
            .Map(company => company?.MapTo<Company, CompanyDto>())
            .Run();
    }

    public async Task<Either<Error,CompanyDto?>> AddCompanyAsync(CreateCompanyDto company, CancellationToken ct = default)
    {
        return await EitherAsync<Error, CreateCompanyDto>
            .FromRight(company)
            .Ensure(c => !string.IsNullOrEmpty(c.Name), new Error("name can't be null or empty."))
            .Ensure(c => !string.IsNullOrEmpty(c.Email), new Error("email can't be null or empty."))
            .Ensure(c => !string.IsNullOrEmpty(c.Phone), new Error("phone can't be null or empty."))
            .Ensure(c => !string.IsNullOrEmpty(c.LegalName), new Error("legal name can't be null or empty."))
            .Map(c => c.Project())
            .FlatMap(async companyToAdd =>
            {
                await unitOfWork.Companies.AddCompanyAsync(companyToAdd, ct);
                await unitOfWork.CommitAsync(ct);
                return companyToAdd;
            }, ex => new Error(ex.Message))
            .Map(companySaved => companySaved?.MapTo<Company, CompanyDto>())
            .Run();
    }

    public async Task<Either<Error,CompanyDto?>> UpdateCompanyAsync(int companyId, UpdateCompanyDto company, CancellationToken ct = default)
    {

        return await EitherAsync<Error, int>
            .FromRight(companyId)
            .Ensure(x => x > 0, new Error("id must be greater than zero"))
            .Ensure(_ => !string.IsNullOrWhiteSpace(company.Name), new Error("name can't be null or empty."))
            .Ensure(_ => !string.IsNullOrWhiteSpace(company.Email), new Error("email can't be null or empty."))
            .Ensure(_ => !string.IsNullOrWhiteSpace(company.Phone), new Error("phone can't be null or empty."))
            .Ensure(_ => !string.IsNullOrWhiteSpace(company.LegalName), new Error("legal name can't be null or empty."))
            .FlatMap(async searchId => await unitOfWork.Companies.GetCompanyByIdAsync(searchId, ct),
                error => new Error(error.Message))
            .Ensure(companyDb => companyDb is not null, new Error("company not found"))
            .Map(companyToUpdate =>
            {
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
                return companyToUpdate;
            }).FlatMap(async companyToUpdate =>
            {
                await unitOfWork.Companies.UpdateCompanyAsync(companyToUpdate!, ct);
                await unitOfWork.CommitAsync(ct);
                return companyToUpdate;
            }, error => new Error(error.Message))
            .Map(companyDb => companyDb?.MapTo<Company, CompanyDto>())
            .Run();
    }

    public async Task DeleteCompanyAsync(int id, CancellationToken ct = default) 
        => await  unitOfWork.Companies.DeleteCompanyAsync(id, ct);
    
    public async Task<bool> ExistsCompanyAsync(int id, CancellationToken ct = default) 
        => await unitOfWork.Companies.ExistsAsync(c=>c.Id == id, ct);
}