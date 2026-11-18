using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.CompanyIssuerDtos;

// The RNC of a branch does not change: the certificate and the authorized
// ranges are bound to it. What changes is the legal name and whether the
// branch still issues.
public record UpdateCompanyIssuerRequest(string LegalName, bool IsActive) : IMapTo<CompanyIssuer>
{
    public CompanyIssuer MapTo()
        => new()
        {
            LegalName = LegalName.Trim(),
            Status = IsActive ? EntityStatus.Active : EntityStatus.Inactive
        };
}
