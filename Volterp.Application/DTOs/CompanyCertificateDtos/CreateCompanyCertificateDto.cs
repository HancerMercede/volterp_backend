using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.CompanyCertificateDtos;

public record CreateCompanyCertificateDto(
    string Name,
    string TaxId,
    DateTime ExpiresAt,
    string Credential) : IMapTo<CompanyCertificate>
{
    public CompanyCertificate MapTo()
    {
        // The credential is NOT copied here — the service encrypts it before it
        // reaches the entity. CompanyId is not copied either: the controller
        // reads it from the JWT.
        return new CompanyCertificate
        {
            Name = Name,
            TaxId = TaxId,
            ExpiresAt = ExpiresAt
        };
    }
}
