namespace Volterp.ElectronicInvoicing.Dgii;

/// <summary>
/// Thrown when the DGII answers with something we cannot turn into a result.
/// The raw body travels with it: the DGII explains itself in the body, not in
/// the status code, and losing it makes every failure a guessing game.
/// </summary>
public sealed class DgiiException : Exception
{
    public DgiiException(string message, string? rawBody = null, Exception? innerException = null)
        : base(message, innerException)
    {
        RawBody = rawBody;
    }

    /// <summary>The body the DGII returned, verbatim.</summary>
    public string? RawBody { get; }
}
