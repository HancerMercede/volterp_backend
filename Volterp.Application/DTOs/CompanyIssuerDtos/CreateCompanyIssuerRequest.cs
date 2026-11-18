using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.CompanyIssuerDtos;

// The branch of the tenant that issues: the RNC that signs the documents
// and the legal name the DGII reads.
public record CreateCompanyIssuerRequest(string TaxId, string LegalName) : IMapTo<CompanyIssuer>
{
    public CompanyIssuer MapTo()
        => new()
        {
            TaxId = TaxId,
            LegalName = LegalName.Trim()
        };
}
