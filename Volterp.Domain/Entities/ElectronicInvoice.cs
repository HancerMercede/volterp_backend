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

    // The unsigned XML stays beside the signed one so the signature can be audited.
    public string? OriginalXml { get; set; }
    public string SignedXml { get; set; } = string.Empty;
    public DateTime? SignedAt { get; set; }
    public string? TrackId { get; set; }
    public string? SecurityCode { get; set; }
    public string? QrUrl { get; set; }
    public DateTime? SentAt { get; set; }

    public EcfStatus Status { get; set; } = EcfStatus.Draft;
    public string? VerdictNote { get; set; }

    // The appointment of the next verdict check and the number of checks the
    // document already carries: the wait grows with it.
    public DateTime? NextCheckAt { get; set; }
    public int StatusCheckCount { get; set; }

    // The worker that took the document and the moment its hold ends: a
    // worker that dies leaves the document to the next one.
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseUntil { get; set; }

    // The attempts the document already had and the moment the next one is
    // due.
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }

    // The document is out of the automatic queue and a human has to take it.
    public bool RequiresReview { get; set; }

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

        // The document is with the DGII but nobody asked for its verdict yet:
        // the appointment is what keeps it from being forgotten.
        NextCheckAt = sentAt;
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
        NextCheckAt = null;
    }

    public void AcceptConditionally(string observation)
    {
        RequirePendingVerdict(EcfStatus.AcceptedConditionally);

        if (string.IsNullOrWhiteSpace(observation))
            throw new ArgumentException("The observation cannot be empty.", nameof(observation));

        VerdictNote = observation;
        Status = EcfStatus.AcceptedConditionally;
        NextCheckAt = null;
    }

    public void Reject(string reason)
    {
        // The DGII refuses the document either at reception, before it is
        // sent, or in the validation that follows.
        if (Status is not (EcfStatus.Signed or EcfStatus.Sent or EcfStatus.InProcess))
            throw InvalidTransition(EcfStatus.Rejected);

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("The rejection reason cannot be empty.", nameof(reason));

        VerdictNote = reason;
        Status = EcfStatus.Rejected;
        NextCheckAt = null;
    }

    public void Annul()
    {
        if (Status is not (EcfStatus.Accepted or EcfStatus.AcceptedConditionally))
            throw InvalidTransition(EcfStatus.Annulled);

        Status = EcfStatus.Annulled;
        NextCheckAt = null;
    }

    // The DGII is still validating the document, so its verdict is asked
    // again later. A verdict that is already final is not asked again, and
    // the appointment of a document that reached a final verdict is gone.
    public void ScheduleNextCheck(DateTime nextCheckAt)
    {
        // The document waits for a verdict: the DGII said it is validating it
        // or it was received and nobody asked about it yet.
        if (Status is not (EcfStatus.Sent or EcfStatus.InProcess))
            throw InvalidTransition(EcfStatus.InProcess);

        StatusCheckCount++;
        NextCheckAt = nextCheckAt;
    }

    // The hold of the worker ends with its work: the document goes back to
    // the queue and the next worker can take it.
    public void ReleaseLease()
    {
        LeaseOwner = null;
        LeaseUntil = null;
    }

    // The attempt did not reach the DGII and the document waits for the next
    // one.
    public void ScheduleNextAttempt(DateTime nextAttemptAt)
    {
        if (Status != EcfStatus.Signed)
            throw InvalidTransition(EcfStatus.Signed);

        NextAttemptAt = nextAttemptAt;
    }

    // The document is out of the automatic queue: a human has to take it.
    public void MarkForReview(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("The reason cannot be empty.", nameof(reason));

        VerdictNote = reason;
        RequiresReview = true;
    }

    private void RequirePendingVerdict(EcfStatus target)
    {
        if (Status is not (EcfStatus.Sent or EcfStatus.InProcess))
            throw InvalidTransition(target);
    }

    private InvalidElectronicInvoiceStateTransitionException InvalidTransition(EcfStatus target)
        => new(Status.ToString(), target.ToString());
}
