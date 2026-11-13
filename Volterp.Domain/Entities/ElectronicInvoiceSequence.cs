using Volterp.Domain.Abstractions;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.Domain.Entities;

// The range of e-NCF the DGII granted to an issuer, with the position inside
// it. The taxpayer loads the range: the same range serves the electronic and
// the paper documents, so an electronic number never repeats one already
// issued on paper.
public class ElectronicInvoiceSequence : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string IssuerTaxId { get; set; } = string.Empty;
    public EcfDocumentType DocumentType { get; set; }
    public int From { get; set; }
    public int To { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int CurrentSequence { get; set; }
    public Company Company { get; set; } = null!;

    public bool HasRoom => CurrentSequence < To;

    // The types that carry no expiry date (32 and 34) never expire.
    public bool IsExpired(DateTime now)
        => EcfTypeProfiles.For(DocumentType).RequiresSequenceExpiry
           && ExpiresAt is not null
           && ExpiresAt.Value.Date < now.Date;

    public static string BuildEncf(EcfDocumentType documentType, int sequence)
        => $"E{(int)documentType:D2}{sequence:D10}";
}
