namespace Volterp.Application.DTOs;

public record CompanyDto(
    int Id,
    string Name,
    string TaxId,
    string? LogoUrl,
    bool IsActive,
    string Address,
    string LegalName,
    string Phone,
    string Email
);

public record CreateCompanyDto(
    string Name,
    string TaxId,
    string? LogoUrl,
    string Address,
    string LegalName,
    string Phone,
    string Email
);

public record UpdateCompanyDto(
    string Name,
    string TaxId,
    string? LogoUrl,
    string Address,
    string LegalName,
    string Phone,
    string Email
);