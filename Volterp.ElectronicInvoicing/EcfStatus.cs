namespace Volterp.ElectronicInvoicing;

/// <summary>
/// The lifecycle of an e-CF. The first three are ours, before the document
/// exists for the DGII; the rest are the DGII's own verdicts.
/// </summary>
public enum EcfStatus
{
    Draft = 0,
    Signed = 1,
    Sent = 2,
    InProcess = 3,
    Accepted = 4,
    AcceptedConditionally = 5,
    Rejected = 6,
    Annulled = 7
}
