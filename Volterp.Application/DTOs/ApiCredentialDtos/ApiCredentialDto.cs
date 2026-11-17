using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.ApiCredentialDtos;

// The credential of a tenant as the list shows it: the selector is public
// and the secret is not here. The secret is shown once, when the credential
// is issued.
public record ApiCredentialDto : IMapFrom<ApiCredential>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public EntityStatus Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public void MapFrom(ApiCredential source)
    {
        Id = source.Id;
        Name = source.Name;
        KeyId = source.KeyId;
        Status = source.Status;
        ExpiresAt = source.ExpiresAt;
        IsExpired = source.IsExpired;
        CreatedAt = source.CreatedAt;
        UpdatedAt = source.UpdatedAt;
    }
}
