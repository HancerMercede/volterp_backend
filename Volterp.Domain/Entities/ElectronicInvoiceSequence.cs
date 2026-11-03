using Volterp.Domain.Abstractions;
using Volterp.Domain.Enums;

namespace Volterp.Domain.Entities;

public class ElectronicInvoiceSequence : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public SaleDocumentType DocumentType { get; set; }
    public int CurrentSequence { get; set; }

    public Company Company { get; set; } = null!;

    public static string BuildEncf(SaleDocumentType documentType, int sequence)
        => $"E{(int)documentType:D2}{sequence:D10}";
}
