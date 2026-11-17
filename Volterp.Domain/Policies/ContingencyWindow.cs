namespace Volterp.Domain.Policies;

// The windows of the "Instructivo de Contingencia de Facturación
// Electrónica" of the DGII. They are the norm's, not the code's: the queue
// reads them instead of inventing deadlines of its own.
public sealed class ContingencyWindow
{
    private static readonly TimeSpan OfflineSubmission = TimeSpan.FromHours(72);
    private static readonly TimeSpan ContingencyCeiling = TimeSpan.FromDays(15);

    // The e-CF that was generated without connectivity has to reach the DGII
    // within seventy-two hours.
    public DateTime DeadlineToSend(DateTime issuedAt) => issuedAt + OfflineSubmission;

    // The contingency cannot last more than fifteen calendar days.
    public DateTime EndOfTheContingency(DateTime issuedAt) => issuedAt + ContingencyCeiling;

    // A document that waited longer than the window is not sent again on its
    // own: a human has to take it.
    public bool PassedTheWindow(DateTime issuedAt, DateTime now) => now > DeadlineToSend(issuedAt);

    // The norm does not allow the contingency to last more than fifteen
    // days: the replacement of a paper document that waits longer than that
    // needs a human.
    public bool PassedTheCeiling(DateTime contingencyStartsAt, DateTime now)
        => now > EndOfTheContingency(contingencyStartsAt);

    // The queue does not even look at the documents that are older than the
    // ceiling of the contingency.
    public DateTime QueueCutoff(DateTime now) => now - ContingencyCeiling;
}
