using EitherWay;
using Microsoft.EntityFrameworkCore;
using Volterp.Application.DTOs.CompanyDtos;
using Volterp.Application.DTOs.UserDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;

namespace Volterp.Tools.Onboarding;

public sealed class TenantOnboardingService(
    IUnitOfWork unitOfWork,
    ICompanyService companyService,
    IUserService userService,
    IPasswordGenerator passwords) : ITenantOnboarding
{
    public async Task<Either<Error, TenantOnboardingOutcome>> RunAsync(
        TenantOnboardingRequest request,
        CancellationToken ct = default)
    {
        // The RNC is the fiscal identity of the tenant and the header of every
        // e-CF it issues: it is validated with the value object of the domain
        // before anything is written, so an invalid one leaves the database
        // untouched.
        if (!Rnc.TryParse(request.TaxId, out var rnc))
            return Failure("The tax id must be a valid RNC or citizen ID.");

        var existing = await unitOfWork.Companies.GetByCondictionsAsync(
            c => c.TaxId == rnc.Value,
            ct,
            query => query.Include(c => c.Users));

        // The company with that RNC is already there and it has its
        // administrator: there is nothing to create.
        if (existing is not null && existing.Users.Count > 0)
            return Success(new TenantOnboardingOutcome(
                true,
                existing.Id,
                existing.Name,
                existing.TaxId,
                request.AdminUsername,
                null));

        // The checks of the administrator run before anything is written: a
        // tenant without the user that could administer it cannot be repaired
        // by running the command again, because the second run finds the
        // company already there and does nothing.
        if (await unitOfWork.Users.ExistsByUsernameAsync(request.AdminUsername, ct))
            return Failure("The username of the administrator is already in use.");

        if (await unitOfWork.Users.GetByUserByEmailAsync(request.AdminEmail, ct) is not null)
            return Failure("The email address of the administrator is already in use.");

        var companyId = existing?.Id;
        var companyName = existing?.Name;
        var companyTaxId = existing?.TaxId;

        if (existing is null)
        {
            var created = await companyService.AddCompanyAsync(
                new CreateCompanyDto(
                    request.Name,
                    rnc.Value,
                    null,
                    request.Address,
                    request.LegalName,
                    request.Phone,
                    request.Email),
                null,
                ct);

            CompanyDto? createdCompany = null;
            string? companyFailure = null;

            created.Match(
                error =>
                {
                    companyFailure = error.Message;
                    return false;
                },
                saved =>
                {
                    createdCompany = saved;
                    return true;
                });

            if (companyFailure is not null || createdCompany is null)
                return Failure(companyFailure ?? "The company could not be created.");

            companyId = createdCompany.Id;
            companyName = createdCompany.Name;
            companyTaxId = createdCompany.TaxId;
        }

        var password = passwords.Generate();

        var admin = await userService.CreateAsync(
            new CreateUserDto(
                request.AdminUsername,
                password,
                request.AdminEmail,
                request.AdminFullName,
                [UserRole.SuperAdmin]),
            companyId!.Value,
            null,
            ct);

        UserDto? createdAdmin = null;
        string? adminFailure = null;

        admin.Match(
            error =>
            {
                adminFailure = error.Message;
                return false;
            },
            saved =>
            {
                createdAdmin = saved;
                return true;
            });

        if (adminFailure is not null || createdAdmin is null)
            return Failure(adminFailure ?? "The administrator could not be created.");

        return Success(new TenantOnboardingOutcome(
            existing is not null,
            companyId!.Value,
            companyName!,
            companyTaxId!,
            createdAdmin.Username,
            password));
    }

    private static Either<Error, TenantOnboardingOutcome> Failure(string message)
        => new Either<Error, TenantOnboardingOutcome>.Left(new Error(message));

    private static Either<Error, TenantOnboardingOutcome> Success(TenantOnboardingOutcome outcome)
        => new Either<Error, TenantOnboardingOutcome>.Right(outcome);
}
