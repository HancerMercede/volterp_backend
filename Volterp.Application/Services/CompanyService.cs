using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.DTOs.CompanyDtos;
using Volterp.Application.Exceptions.AppErrors;
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

    public async Task<Either<Error,CompanyDto?>> GetCompanyByIdAsync(Guid id, CancellationToken ct = default)
    {
         return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty"))
            .FlatMap(async companyId => await unitOfWork.Companies.GetCompanyByIdAsync(companyId, ct),
                error => new Error(error.Message))
            .Ensure(company => company is not null, new Error("company not found"))
            .Map(company => company?.MapTo<Company, CompanyDto>())
            .Run();
    }

    public async Task<Either<Error,CompanyDto?>> AddCompanyAsync(CreateCompanyDto company, Guid? userId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, CreateCompanyDto>
            .FromRight(company)
            .Ensure(c => !string.IsNullOrEmpty(c.Name), new Error("name can't be null or empty."))
            .Ensure(c => !string.IsNullOrEmpty(c.Email), new Error("email can't be null or empty."))
            .Ensure(c => !string.IsNullOrEmpty(c.Phone), new Error("phone can't be null or empty."))
            .Ensure(c => !string.IsNullOrEmpty(c.LegalName), new Error("legal name can't be null or empty."))
            .Map(c => c.Project())
            .Map(companyToAdd =>
            {
                if (userId.HasValue) companyToAdd.SetCreatedBy(userId.Value);
                return companyToAdd;
            })
            .FlatMap(async companyToAdd =>
            {
                await unitOfWork.Companies.AddCompanyAsync(companyToAdd, ct);
                await unitOfWork.CommitAsync(ct);
                return companyToAdd;
            }, ex => new Error(ex.Message))
            .Map(companySaved => companySaved?.MapTo<Company, CompanyDto>())
            .Run();
    }

    public async Task<Either<Error,CompanyDto?>> UpdateCompanyAsync(Guid companyId, UpdateCompanyDto company, Guid? userId, CancellationToken ct = default)
    {

        return await EitherAsync<Error, Guid>
            .FromRight(companyId)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty"))
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
                    if (userId.HasValue) c.SetUpdatedBy(userId.Value);
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

    public async Task<Either<Error, Unit>> DeleteCompanyAsync(Guid id, CancellationToken ct = default)
        => await EitherAsync<Error, Guid>.FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async _ =>
            {
                await unitOfWork.Companies.DeleteCompanyAsync(id, ct);
                await unitOfWork.CommitAsync(ct);
                return new Unit();
            }, ex => new Error(ex.Message))
            .Run();


    public async Task<Either<Error, bool>> ExistsCompanyAsync(Guid id, CancellationToken ct = default)
        => await EitherAsync<Error, Guid>.FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("id must not be empty."))
            .FlatMap(async companyId => await unitOfWork.Companies.ExistsAsync(c => c.Id == companyId, ct)
             ,ex => new Error(ex.Message))
            .Ensure(companyExist => companyExist is not false, new Error("company not found"))
            .Run();
}
