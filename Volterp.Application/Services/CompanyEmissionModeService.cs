using EitherWay;
using Volterp.Application.DTOs.CompanyDtos.EmissionMode;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.Services;

public class CompanyEmissionModeService(IUnitOfWork unitOfWork)
{
    public async Task<Either<Error, CompanyEmissionModeDto>> GetAsync(
        Guid companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, Guid>
            .FromRight(companyId)
            .Ensure(id => id != Guid.Empty, new Error("companyId must not be empty."))
            .FlatMap(async id => await unitOfWork.Companies.GetCompanyByIdAsync(id, ct),
                ex => new Error(ex.Message))
            .Ensure(company => company is not null, new Error("Company not found."))
            .Map(company => ToDto(company!))
            .Run();
    }

    public async Task<Either<Error, CompanyEmissionModeDto>> UpdateAsync(
        UpdateCompanyEmissionModeDto request,
        Guid companyId,
        Guid? userId = null,
        CancellationToken ct = default)
    {
        return await EitherAsync<Error, UpdateCompanyEmissionModeDto>
            .FromRight(request)
            .Ensure(r => r.EmissionMode is CompanyEmissionMode.Electronic or CompanyEmissionMode.Paper,
                new Error("The emission mode is not valid."))
            .FlatMap(async _ => await unitOfWork.Companies.GetCompanyByIdAsync(companyId, ct),
                ex => new Error(ex.Message))
            .Ensure(company => company is not null, new Error("Company not found."))
            .Map(company =>
            {
                company!.EmissionMode = request.EmissionMode;
                company.MarkUpdated();
                if (userId.HasValue)
                    company.SetUpdatedBy(userId.Value);
                return company;
            })
            .FlatMap(async company =>
            {
                await unitOfWork.Companies.UpdateCompanyAsync(company, ct);
                await unitOfWork.CommitAsync(ct);
                return company;
            }, ex => new Error(ex.Message))
            .Map(ToDto)
            .Run();
    }

    public static bool UsesElectronicEmission(Company? company, DateTime now)
    {
        // A company that cannot be read cannot silently turn an electronic
        // sale into paper. The missing company stays on the electronic path,
        // where the existing certificate check fails closed.
        if (company is null)
            return true;

        if (company.EmissionMode == CompanyEmissionMode.Paper)
            return false;

        if (company.ContingencyStatus == CompanyContingencyStatus.None)
            return true;

        if (!company.ContingencyStartsAt.HasValue || !company.ContingencyEndsAt.HasValue)
            return false;

        return now < company.ContingencyStartsAt.Value
            || now >= company.ContingencyEndsAt.Value;
    }

    private static CompanyEmissionModeDto ToDto(Company company)
        => new(
            company.EmissionMode,
            company.ContingencyStatus,
            company.ContingencyStartsAt,
            company.ContingencyEndsAt);
}
