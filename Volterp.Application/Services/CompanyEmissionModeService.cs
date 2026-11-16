using EitherWay;
using Volterp.Application.DTOs.CompanyDtos.EmissionMode;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.Policies;

namespace Volterp.Application.Services;

public class CompanyEmissionModeService(IUnitOfWork unitOfWork) : ICompanyEmissionModeService
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
            .Ensure(r => r.EmissionMode is CompanyEmissionMode.Electronic or CompanyEmissionMode.Normal,
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

    public async Task<Either<Error, CompanyEmissionModeDto>> UpdateContingencyAsync(
        UpdateCompanyContingencyDto request,
        Guid companyId,
        Guid? userId = null,
        CancellationToken ct = default)
    {
        return await EitherAsync<Error, UpdateCompanyContingencyDto>
            .FromRight(request)
            .Ensure(r => r.Status is CompanyContingencyStatus.None
                    or CompanyContingencyStatus.Total
                    or CompanyContingencyStatus.Partial,
                new Error("The contingency status is not valid."))
            .Ensure(r => r.Status == CompanyContingencyStatus.None || (r.StartsAt.HasValue && r.EndsAt.HasValue),
                new Error("The contingency needs its entry and its exit."))
            .Ensure(r => r.Status == CompanyContingencyStatus.None || r.StartsAt < r.EndsAt,
                new Error("The contingency ends after it starts."))
            .Ensure(r => r.Status == CompanyContingencyStatus.None
                    || r.EndsAt <= new ContingencyWindow().EndOfTheContingency(r.StartsAt!.Value),
                new Error("The contingency cannot last more than fifteen days."))
            .FlatMap(async _ => await unitOfWork.Companies.GetCompanyByIdAsync(companyId, ct),
                ex => new Error(ex.Message))
            .Ensure(company => company is not null, new Error("Company not found."))
            .Map(company =>
            {
                company!.DeclareContingency(request.Status, request.StartsAt, request.EndsAt);
                company.MarkUpdated();
                if (userId.HasValue) company.SetUpdatedBy(userId.Value);
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

    private static CompanyEmissionModeDto ToDto(Company company)
        => new(
            company.EmissionMode,
            company.ContingencyStatus,
            company.ContingencyStartsAt,
            company.ContingencyEndsAt);
}
