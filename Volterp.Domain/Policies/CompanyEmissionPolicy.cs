using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Domain.Policies;

// The path a sale takes: the taxpayer declares how it invoices and the
// contingency suspends the electronic path for a while. The rule is the
// domain's, not the application's: the service and the sale read the same
// one.
public static class CompanyEmissionPolicy
{
    public static bool UsesElectronicEmission(Company? company, DateTime now)
    {
        // A company that cannot be read cannot silently turn an electronic
        // sale into the normal one. The missing company stays on the
        // electronic path, where the existing certificate check fails closed.
        if (company is null)
            return true;

        if (company.EmissionMode == CompanyEmissionMode.Normal)
            return false;

        if (company.ContingencyStatus == CompanyContingencyStatus.None)
            return true;

        // A contingency without both of its bounds suspends the electronic
        // path in the conservative way.
        if (!company.ContingencyStartsAt.HasValue || !company.ContingencyEndsAt.HasValue)
            return false;

        // Outside the window of the contingency the electronic path is back.
        return now < company.ContingencyStartsAt.Value
            || now >= company.ContingencyEndsAt.Value;
    }
}
