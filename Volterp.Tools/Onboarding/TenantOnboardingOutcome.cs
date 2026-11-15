namespace Volterp.Tools.Onboarding;

// What the command did. The password of the administrator travels here and
// nowhere else: it is shown once, on the screen of whoever ran the command.
public sealed record TenantOnboardingOutcome(
    bool CompanyAlreadyExisted,
    Guid CompanyId,
    string CompanyName,
    string TaxId,
    string AdminUsername,
    string? AdminPassword);
