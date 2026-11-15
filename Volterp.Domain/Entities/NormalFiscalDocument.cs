using Volterp.Domain.Abstractions;
using Volterp.Domain.Enums;

namespace Volterp.Domain.Entities;

// The fiscal record of a comprobante issued on paper. The number comes from
// the same authorized range the electronic path consumes, so a number used on
// paper is never handed out again.
public class NormalFiscalDocument : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SaleId { get; set; }

    public string Number { get; set; } = string.Empty;
    public NormalFiscalDocumentType DocumentType { get; set; }
    public string IssuerTaxId { get; set; } = string.Empty;
    public string? ReceiverTaxId { get; set; }
    public string? ReceiverName { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Itbis { get; set; }
    public decimal Total { get; set; }
    public DateTime IssuedAt { get; set; }

    // The number of a normal document: the letter, the normal type and the
    // position inside the range. It is eleven characters long; the thirteen
    // of the e-NCF belong to the electronic document alone.
    public static string BuildNumber(NormalFiscalDocumentType documentType, int sequence)
        => $"B{(int)documentType:D2}{sequence:D8}";
}
