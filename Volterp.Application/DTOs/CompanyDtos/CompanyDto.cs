using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.CompanyDtos;

public class CompanyDto:IMapFrom<Company>
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IsActive  { get; set; }
    public string Address   { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string Phone { get; set; }  = string.Empty;
    public string Email { get; set; }  = string.Empty;
    public void MapFrom(Company source)
    {
        Id =  source.Id;
        Name = source.Name;
        TaxId = source.TaxId;
        LogoUrl = source.LogoUrl;
        IsActive = source.IsActive;
        Address = source.Address;
        LegalName = source.LegalName;
        Phone = source.Phone;
        Email = source.Email;
    }
}


