using Volterp.Domain.Abstractions;
using Volterp.Domain.Exceptions;
using Volterp.ElectronicInvoicing;

namespace Volterp.Domain.Entities;

public class ElectronicInvoice : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SaleId { get; set; }

    public string ENCF { get; set; } = string.Empty;
    public EcfDocumentType DocumentType { get; set; } = EcfDocumentType.Consumer;
    public string IssuerTaxId { get; set; } = string.Empty;
    public string? ReceiverTaxId { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Itbis { get; set; }
    public decimal Total { get; set; }

    public string SignedXml { get; set; } = string.Empty;
    public DateTime? SignedAt { get; set; }
    public string? TrackId { get; set; }
    public string? SecurityCode { get; set; }
    public string? QrUrl { get; set; }
    public DateTime? SentAt { get; set; }

    public EcfStatus Status { get; set; } = EcfStatus.Draft;
    public string? VerdictNote { get; set; }

    public Sale Sale { get; set; } = null!;
    public Company Company { get; set; } = null!;

    /// <summary>
    /// Whether the DGII already accepted the document. An issued sale is not
    /// edited nor deleted, it is corrected with a note.
    /// </summary>
    public bool IsIssued => Status is EcfStatus.Accepted or EcfStatus.AcceptedConditionally;

    public void Sign(string signedXml, DateTime signedAt)
    {
        if (Status != EcfStatus.Draft)
            throw InvalidTransition(EcfStatus.Signed);

        if (string.IsNullOrWhiteSpace(signedXml))
            throw new ArgumentException("The signed XML cannot be empty.", nameof(signedXml));

        SignedXml = signedXml;
        SignedAt = signedAt;
        Status = EcfStatus.Signed;
    }

    public void MarkSent(string? trackId, DateTime sentAt)
    {
        if (Status != EcfStatus.Signed)
            throw InvalidTransition(EcfStatus.Sent);

        TrackId = trackId;
        SentAt = sentAt;
        Status = EcfStatus.Sent;
    }

    public void MarkInProcess()
    {
        if (Status is not (EcfStatus.Sent or EcfStatus.InProcess))
            throw InvalidTransition(EcfStatus.InProcess);

        Status = EcfStatus.InProcess;
    }

    public void Accept()
    {
        RequirePendingVerdict(EcfStatus.Accepted);

        Status = EcfStatus.Accepted;
    }

    public void AcceptConditionally(string observation)
    {
        RequirePendingVerdict(EcfStatus.AcceptedConditionally);

        if (string.IsNullOrWhiteSpace(observation))
            throw new ArgumentException("The observation cannot be empty.", nameof(observation));

        VerdictNote = observation;
        Status = EcfStatus.AcceptedConditionally;
    }

    public void Reject(string reason)
    {
        RequirePendingVerdict(EcfStatus.Rejected);

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("The rejection reason cannot be empty.", nameof(reason));

        VerdictNote = reason;
        Status = EcfStatus.Rejected;
    }

    public void Annul()
    {
        if (Status is not (EcfStatus.Accepted or EcfStatus.AcceptedConditionally))
            throw InvalidTransition(EcfStatus.Annulled);

        Status = EcfStatus.Annulled;
    }

    private void RequirePendingVerdict(EcfStatus target)
    {
        if (Status is not (EcfStatus.Sent or EcfStatus.InProcess))
            throw InvalidTransition(target);
    }

    private InvalidElectronicInvoiceStateTransitionException InvalidTransition(EcfStatus target)
        => new(Status.ToString(), target.ToString());
}
