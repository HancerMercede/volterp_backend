using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.CompanyIssuerDtos;

public record CompanyIssuerDto : IMapFrom<CompanyIssuer>
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string TaxId { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public void MapFrom(CompanyIssuer source)
    {
        Id = source.Id;
        CompanyId = source.CompanyId;
        TaxId = source.TaxId;
        LegalName = source.LegalName;
        IsActive = source.IsActive;
    }
}
