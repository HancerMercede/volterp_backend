namespace Volterp.ElectronicInvoicing;

// What the tenant sees on the fiscal side of a document: the states that are
// still open. The closed verdicts are not here because there is nothing left
// to do about them.
public enum EcfFiscalState
{
    PendingEmission = 1,
    InProcess = 2,
    Rejected = 3,
    RequiresReview = 4
}

// The classification of a document into the state the tenant sees. It lives
// in the library because the count and the list of the surface are two uses
// of the same answer: with two rules they could disagree.
public static class EcfFiscalStateClassifier
{
    // The verdicts that close the document. The query of the open documents
    // filters by this set and the classifier answers nothing for it, so the
    // set of closed documents is written once.
    public static IReadOnlyList<EcfStatus> ClosedStatuses { get; } =
    [
        EcfStatus.Accepted,
        EcfStatus.AcceptedConditionally,
        EcfStatus.Annulled
    ];

    // A document that is not closed is open, and it is in exactly one of the
    // four states.
    public static EcfFiscalState? Classify(EcfStatus status, bool requiresReview)
    {
        if (ClosedStatuses.Contains(status))
            return null;

        return status switch
        {
            // The nullity of the document dominates: what the DGII said about
            // it is what the tenant has to read.
            EcfStatus.Rejected => EcfFiscalState.Rejected,

            // The document is out of the automatic queue: a human has to take
            // it, whatever the state it carries.
            _ when requiresReview => EcfFiscalState.RequiresReview,

            // Signed and waiting in the queue: it has not travelled to the
            // DGII yet.
            EcfStatus.Signed => EcfFiscalState.PendingEmission,

            // The DGII has it and has not validated it yet.
            EcfStatus.Sent or EcfStatus.InProcess => EcfFiscalState.InProcess,

            // A draft that does not require review should not exist. It is
            // treated as a document a human has to take, never as a document
            // with nothing to do.
            _ => EcfFiscalState.RequiresReview
        };
    }
}
