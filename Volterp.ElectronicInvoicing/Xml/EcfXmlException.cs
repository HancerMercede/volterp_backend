using Volterp.ElectronicInvoicing.Schemas;

namespace Volterp.ElectronicInvoicing.Xml;

/// <summary>
/// Thrown when a document cannot be written as a valid e-CF: either it misses
/// something its type requires, or it does not match the official schema.
/// </summary>
public sealed class EcfXmlException : Exception
{
    /// <summary>
    /// Creates the exception with the breaches the schema found.
    /// </summary>
    public EcfXmlException(string message, IReadOnlyList<EcfSchemaViolation> violations)
        : base(message)
    {
        Violations = violations;
    }

    /// <summary>
    /// Creates the exception for a document that misses something its type
    /// requires.
    /// </summary>
    public EcfXmlException(string message) : base(message)
    {
    }

    /// <summary>
    /// The breaches the official schema found, empty when the document was
    /// rejected before it could be checked.
    /// </summary>
    public IReadOnlyList<EcfSchemaViolation> Violations { get; } = [];
}
