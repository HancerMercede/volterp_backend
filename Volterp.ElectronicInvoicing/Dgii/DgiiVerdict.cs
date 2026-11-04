namespace Volterp.ElectronicInvoicing.Dgii;

/// <summary>
/// The validation verdict the DGII grants to a received e-CF. The numeric codes
/// are the ones the DGII publishes; they are not an enum of ours, so they are
/// mapped explicitly rather than cast.
/// </summary>
public enum DgiiVerdict
{
    /// <summary>The track id was not found in the DGII records.</summary>
    NotFound = 0,

    /// <summary>The e-CF is valid.</summary>
    Accepted = 1,

    /// <summary>The e-CF is null for tax purposes because it failed somewhere.</summary>
    Rejected = 2,

    /// <summary>Not validated yet: poll again after a prudent wait.</summary>
    InProcess = 3,

    /// <summary>It failed somewhere but was not rejected, so it is still valid.</summary>
    AcceptedConditionally = 4
}

public static class DgiiVerdictExtensions
{
    /// <summary>
    /// Translates the DGII verdict into the state of our document.
    /// A track id the DGII does not know has no document state: it is a failed
    /// query, not a verdict, so it maps to nothing.
    /// </summary>
    public static EcfStatus? ToEcfStatus(this DgiiVerdict verdict) => verdict switch
    {
        DgiiVerdict.Accepted => EcfStatus.Accepted,
        DgiiVerdict.AcceptedConditionally => EcfStatus.AcceptedConditionally,
        DgiiVerdict.Rejected => EcfStatus.Rejected,
        DgiiVerdict.InProcess => EcfStatus.InProcess,
        _ => null
    };
}
