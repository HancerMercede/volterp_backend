using Volterp.Domain.Abstractions;
using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;

namespace Volterp.Domain.Entities;

public class CompanyCertificate : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    public string EncryptedCredential { get; set; } = string.Empty;

    public Company Company { get; set; } = null!;

    public bool IsExpired => ExpiresAt <= DateTime.UtcNow;

    public void UpdateTaxId(Rnc rnc) => TaxId = rnc.Value;
}
