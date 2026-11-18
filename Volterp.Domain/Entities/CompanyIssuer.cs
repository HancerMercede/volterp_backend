using Volterp.Domain.Abstractions;
using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;

namespace Volterp.Domain.Entities;

// An issuer RNC of the tenant: a branch that issues on behalf of the
// company. The certificate belongs to this RNC and not to the tenant, and
// the sale names the RNC that issues it.
public class CompanyIssuer : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string TaxId { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public EntityStatus Status { get; set; } = EntityStatus.Active;

    public Company Company { get; set; } = null!;

    public bool IsActive => Status == EntityStatus.Active;

    public void UpdateTaxId(Rnc rnc) => TaxId = rnc.Value;

    public void UpdateLegalName(string legalName) => LegalName = legalName;

    public void Activate() => Status = EntityStatus.Active;

    public void Deactivate() => Status = EntityStatus.Inactive;
}
