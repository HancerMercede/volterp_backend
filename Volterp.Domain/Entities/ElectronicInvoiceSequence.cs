using Volterp.Domain.Abstractions;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;

namespace Volterp.Domain.Entities;

public class ElectronicInvoiceSequence : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public EcfDocumentType DocumentType { get; set; }
    public int CurrentSequence { get; set; }

    public Company Company { get; set; } = null!;

    public static string BuildEncf(EcfDocumentType documentType, int sequence)
        => $"E{(int)documentType:D2}{sequence:D10}";
}
