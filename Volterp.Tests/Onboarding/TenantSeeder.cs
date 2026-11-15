using EitherWay;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Services;
using Volterp.Infrastructure.UnitOfWork;
using Volterp.Tools.Onboarding;

namespace Volterp.Tests.Onboarding;

// The tenant of the tests is created by the command of the deployment, the
// same way the tenant of a client is created.
public static class TenantSeeder
{
    public static ITenantOnboarding OnboardingOver(IUnitOfWork unitOfWork)
        => new TenantOnboardingService(
            unitOfWork,
            new CompanyService(unitOfWork),
            new UserService(unitOfWork, new PasswordHasher(), new DeploymentCurrentUserService()),
            new PasswordGenerator());

    public static async Task<TenantOnboardingOutcome> SeedAsync(VolterpDbContext context)
    {
        var unitOfWork = new UnitOfWork(context);

        var result = await OnboardingOver(unitOfWork).RunAsync(new TenantOnboardingRequest(
            Name: "Distribuidora del Caribe",
            TaxId: "101500018",
            LegalName: "Distribuidora del Caribe S.R.L.",
            Address: "Av. 27 de Febrero No. 42, Santo Domingo Este",
            Phone: "809-555-0101",
            Email: "contacto@distcaribe.do",
            AdminUsername: "admin",
            AdminEmail: "admin@distcaribe.do",
            AdminFullName: "Administrador"));

        var failure = result.Match(error => error.Message, _ => (string?)null);

        if (failure is not null)
            throw new InvalidOperationException($"The tenant of the test was not created: {failure}");

        return ((Either<Error, TenantOnboardingOutcome>.Right)result).Value;
    }
}
