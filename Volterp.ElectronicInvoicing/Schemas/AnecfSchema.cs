using System.Xml;
using System.Xml.Schema;

namespace Volterp.ElectronicInvoicing.Schemas;

// The official schema of the annulment of the ranges of e-NCF, taken from the
// XSD the DGII publishes. It is a document of its own: its root is ANECF and
// it ends with the slot the digital signature goes into.
public sealed class AnecfSchema
{
    private const string Resource = "ANECF-v1.0.xsd";

    private readonly XmlSchemaSet _schemas;

    private AnecfSchema(XmlSchemaSet schemas) => _schemas = schemas;

    // The schema is loaded once and kept.
    public static AnecfSchema Instance { get; } = Load();

    /// <summary>
    /// Validates an XML document against this schema and returns every breach
    /// it finds. The document has to carry its digital signature: the schema
    /// ends with the slot the signature goes into and it demands one element
    /// there.
    /// </summary>
    public IReadOnlyList<EcfSchemaViolation> Validate(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        var violations = new List<EcfSchemaViolation>();
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = _schemas
        };
        settings.ValidationEventHandler += (_, args) =>
        {
            if (args.Severity == XmlSeverityType.Error)
            {
                var line = args.Exception is XmlSchemaException schemaException ? schemaException.LineNumber : 0;
                violations.Add(new EcfSchemaViolation(args.Message, line));
            }
        };

        using var reader = XmlReader.Create(new StringReader(xml), settings);
        while (reader.Read())
        {
        }

        return violations;
    }

    /// <summary>
    /// Validates a document that has not been signed yet. The schema demands
    /// an element in the slot the digital signature goes into, so a placeholder
    /// stands in for the signature while the content of the document is
    /// checked.
    /// </summary>
    public IReadOnlyList<EcfSchemaViolation> ValidateUnsigned(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        var end = xml.LastIndexOf("</ANECF>", StringComparison.Ordinal);
        if (end < 0)
            return Validate(xml);

        return Validate(xml.Insert(end, "<FirmaReservada />"));
    }

    private static AnecfSchema Load()
    {
        var assembly = typeof(AnecfSchema).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(Resource, StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The schema resource '{Resource}' is missing.");

        using var reader = XmlReader.Create(stream, new XmlReaderSettings { XmlResolver = null });

        var schema = XmlSchema.Read(reader, (_, args) =>
        {
            if (args.Severity == XmlSeverityType.Error)
                throw new InvalidOperationException($"The schema '{Resource}' is not valid: {args.Message}");
        }) ?? throw new InvalidOperationException($"The schema '{Resource}' could not be read.");

        var schemas = new XmlSchemaSet { XmlResolver = null };
        schemas.Add(schema);
        schemas.Compile();

        return new AnecfSchema(schemas);
    }
}
