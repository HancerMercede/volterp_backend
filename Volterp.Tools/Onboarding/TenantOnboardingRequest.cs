namespace Volterp.Tools.Onboarding;

// The data of the tenant that is being created: the company with its fiscal
// identity and the administrator that goes with it.
public sealed record TenantOnboardingRequest(
    string Name,
    string TaxId,
    string LegalName,
    string Address,
    string Phone,
    string Email,
    string AdminUsername,
    string AdminEmail,
    string AdminFullName);
