using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.CompanyCertificateDtos;

public record CompanyCertificateDto : IMapFrom<CompanyCertificate>
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public EntityStatus Status { get; set; }
    public bool IsExpired { get; set; }

    public void MapFrom(CompanyCertificate source)
    {
        Id = source.Id;
        CompanyId = source.CompanyId;
        Name = source.Name;
        TaxId = source.TaxId;
        ExpiresAt = source.ExpiresAt;
        Status = source.Status;
        IsExpired = source.IsExpired;
    }
}
