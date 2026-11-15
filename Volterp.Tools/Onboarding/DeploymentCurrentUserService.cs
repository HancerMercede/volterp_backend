using Volterp.Application.Interfaces;

namespace Volterp.Tools.Onboarding;

// The command runs with the privileges of the operator: it creates the first
// administrator of a tenant, and there is no administrator yet that could
// authorize it.
public sealed class DeploymentCurrentUserService : ICurrentUserService
{
    public bool IsSuperAdmin() => true;

    public int GetCompanyId() => 0;

    public int? GetUserId() => null;
}
