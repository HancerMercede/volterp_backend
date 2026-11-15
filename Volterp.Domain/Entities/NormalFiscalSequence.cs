using Volterp.Domain.Abstractions;
using Volterp.Domain.Enums;

namespace Volterp.Domain.Entities;

// The range of NCF the DGII authorized to an issuer for the normal (paper)
// documents. It is a range of its own: the range of the electronic
// documents lives in another table and the two never share a position.
public class NormalFiscalSequence : AuditEntity, ITenantScoped
{
    // The number of a normal document carries eight digits of sequence, so a
    // range can never go beyond this position.
    public const int MaxSequence = 99_999_999;

    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string IssuerTaxId { get; set; } = string.Empty;
    public NormalFiscalDocumentType DocumentType { get; set; }
    public int From { get; set; }
    public int To { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int CurrentSequence { get; set; }
    public Company Company { get; set; } = null!;

    public bool HasRoom => CurrentSequence < To;

    // A range that starts below one or goes beyond the eight digits of the
    // number cannot be used: the number would not fit the format.
    public bool HasValidBounds => From >= 1 && To <= MaxSequence && From <= To;

    // The types that carry no expiry date never expire.
    public bool IsExpired(DateTime now)
        => NormalFiscalDocumentTypes.RequiresExpiry(DocumentType)
           && ExpiresAt is not null
           && ExpiresAt.Value.Date < now.Date;
}
