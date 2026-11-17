using System.Globalization;
using Volterp.Domain.Abstractions;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.Domain.Entities;

// The range of e-NCF the DGII granted to an issuer, with the position inside
// it. The taxpayer loads the range: it serves the electronic documents, and
// the normal (paper) documents have a range of their own.
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

    // The day the range was annulled and the answer of the DGII: a range
    // that is annulled is closed and can never be used again.
    public DateTime? AnnulledAt { get; set; }
    public string? AnnulmentResponse { get; set; }

    public Company Company { get; set; } = null!;

    public bool HasRoom => CurrentSequence < To;

    public bool IsAnnulled => AnnulledAt is not null;

    // The annulment closes the range: the tail it still had is consumed by
    // it, so no number of the range can be handed out again.
    public void MarkAnnulled(DateTime annulledAt, string? rawResponse)
    {
        if (IsAnnulled)
            throw new InvalidOperationException("The range was already annulled.");

        AnnulledAt = annulledAt;
        AnnulmentResponse = Cut(rawResponse);
        CurrentSequence = To;
    }

    // The types that carry no expiry date (32 and 34) never expire.
    public bool IsExpired(DateTime now)
        => EcfTypeProfiles.For(DocumentType).RequiresSequenceExpiry
           && ExpiresAt is not null
           && ExpiresAt.Value.Date < now.Date;

    public static string BuildEncf(EcfDocumentType documentType, int sequence)
        => $"E{(int)documentType:D2}{sequence:D10}";

    public static int SequenceOf(string encf)
    {
        if (string.IsNullOrEmpty(encf) || encf.Length != 13)
            throw new ArgumentException("The e-NCF must have thirteen characters.", nameof(encf));

        if (!int.TryParse(
                encf.AsSpan(3, 10),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var sequence))
            throw new ArgumentException("The e-NCF does not end in a valid sequence.", nameof(encf));

        return sequence;
    }

    // The body of the DGII is a diagnostic, not a payload: a pathological
    // answer cannot grow the row without limit. The cut says so: whoever
    // reads the answer has to know that it is not the whole one. The room is
    // the one the answer of a document gets.
    private static string? Cut(string? body)
        => body is null || body.Length <= ElectronicInvoice.MaxRawResponseLength
            ? body
            : body[..ElectronicInvoice.MaxRawResponseLength] + "... [truncated]";
}
