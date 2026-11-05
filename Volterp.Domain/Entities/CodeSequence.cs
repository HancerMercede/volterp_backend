using Volterp.Domain.Abstractions;
using Volterp.Domain.Enums;

namespace Volterp.Domain.Entities;

public class CodeSequence : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public CodeEntityType EntityType { get; set; }
    public int CurrentValue { get; set; }
    public Company Company { get; set; } = null!;
}
