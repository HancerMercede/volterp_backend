using EitherWay;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Tools.Onboarding;

public interface ITenantOnboarding
{
    Task<Either<Error, TenantOnboardingOutcome>> RunAsync(
        TenantOnboardingRequest request,
        CancellationToken ct = default);
}
