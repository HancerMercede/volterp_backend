using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.CompanyDtos;

public record UpdateCompanyDto(
    string Name,
    string TaxId,
    string? LogoUrl,
    string Address,
    string LegalName,
    string Phone,
    string Email
):IMapTo<Company>
{
    public Company MapTo()
    {
        return new Company
        {
            Name = Name,
            TaxId = TaxId,
            LogoUrl = LogoUrl,
            Address = Address,
            LegalName = LegalName,
            Phone = Phone,
            Email = Email
        };
    }
}