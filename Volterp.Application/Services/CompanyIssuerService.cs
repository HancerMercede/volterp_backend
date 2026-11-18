using EitherWay;
using Volterp.Application.DTOs.CompanyIssuerDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;

namespace Volterp.Application.Services;

// The issuer RNCs of the tenant: the branches that issue on its behalf. The
// RNC of the tenant itself is not registered here: it is the identity of the
// company.
public class CompanyIssuerService(IUnitOfWork unitOfWork) : ICompanyIssuerService
{
    public async Task<PagedResult<CompanyIssuerDto>> GetAllAsync(
        Guid companyId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var issuers = await unitOfWork.CompanyIssuers.GetAllAsync(
            issuer => issuer.CompanyId == companyId, pageNumber, pageSize, ct);

        return issuers.MapTo<CompanyIssuer, CompanyIssuerDto>();
    }

    public async Task<Either<Error, CompanyIssuerDto>> GetByIdAsync(
        Guid id, Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(id)
            .Ensure(x => x != Guid.Empty, new Error("The id must not be empty."))
            .FlatMap(async issuerId => await unitOfWork.CompanyIssuers.GetByCondictionsAsync(
                issuer => issuer.Id == issuerId && issuer.CompanyId == companyId, ct), ex => new Error(ex.Message))
            .Ensure(issuer => issuer is not null, new Error("The issuer does not exist."))
            .Map(issuer => issuer!.MapTo<CompanyIssuer, CompanyIssuerDto>())
            .Run();
    }

    public async Task<Either<Error, CompanyIssuerDto>> CreateAsync(
        CreateCompanyIssuerRequest request, Guid companyId, Guid? userId, CancellationToken ct = default)
    {
        var company = await unitOfWork.Companies.GetCompanyByIdAsync(companyId, ct);
        var parsed = Rnc.TryParse(request.TaxId, out var rnc);
        var taxId = parsed ? rnc.Value : string.Empty;

        var existing = parsed
            ? await unitOfWork.CompanyIssuers.GetByCondictionsAsync(
                issuer => issuer.CompanyId == companyId && issuer.TaxId == taxId, ct)
            : null;

        return await EitherAsync<Error, CreateCompanyIssuerRequest>
            .FromRight(request)
            .Ensure(r => company is not null, new Error("The company does not exist."))
            .Ensure(r => !string.IsNullOrWhiteSpace(r.LegalName), new Error("The legal name is required."))
            .Ensure(r => parsed, new Error("The tax id must be a valid RNC or citizen ID."))
            .Ensure(r => company is not null && taxId != company.TaxId,
                new Error("The RNC of the company is already an issuer of its own."))
            .Ensure(r => existing is null, new Error("This RNC is already registered as an issuer."))
            .Map(r => r.Project())
            .Map(issuer =>
            {
                issuer.Id = Guid.CreateVersion7();
                issuer.CompanyId = companyId;
                issuer.UpdateTaxId(new Rnc(taxId));

                if (userId.HasValue)
                    issuer.SetCreatedBy(userId.Value);

                return issuer;
            })
            .FlatMap(async issuer =>
            {
                await unitOfWork.CompanyIssuers.AddAsync(issuer, ct);
                await unitOfWork.CommitAsync(ct);
                return issuer;
            }, ex => new Error(ex.Message))
            .Map(issuer => issuer.MapTo<CompanyIssuer, CompanyIssuerDto>())
            .Run();
    }

    public async Task<Either<Error, CompanyIssuerDto>> UpdateAsync(
        Guid id, UpdateCompanyIssuerRequest request, Guid companyId, Guid? userId, CancellationToken ct = default)
    {
        var existing = await unitOfWork.CompanyIssuers.GetByCondictionsAsync(
            issuer => issuer.Id == id && issuer.CompanyId == companyId, ct);

        return await EitherAsync<Error, UpdateCompanyIssuerRequest>
            .FromRight(request)
            .Ensure(r => existing is not null, new Error("The issuer does not exist."))
            .Ensure(r => !string.IsNullOrWhiteSpace(r.LegalName), new Error("The legal name is required."))
            .Map(r =>
            {
                var issuer = existing!;

                issuer.Apply(x =>
                {
                    x.LegalName = r.LegalName.Trim();
                    x.Status = r.IsActive ? EntityStatus.Active : EntityStatus.Inactive;
                    x.UpdatedAt = DateTime.UtcNow;

                    if (userId.HasValue)
                        x.SetUpdatedBy(userId.Value);
                });

                return issuer;
            })
            .FlatMap(async issuer =>
            {
                await unitOfWork.CompanyIssuers.UpdateAsync(issuer, ct);
                await unitOfWork.CommitAsync(ct);
                return issuer;
            }, ex => new Error(ex.Message))
            .Map(issuer => issuer.MapTo<CompanyIssuer, CompanyIssuerDto>())
            .Run();
    }
}
