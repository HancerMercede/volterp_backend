using System.Collections.Concurrent;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;

namespace Volterp.ElectronicInvoicing.Schemas;

/// <summary>
/// A breach of the official schema of an e-CF type.
/// </summary>
/// <param name="Message">What the schema says about it.</param>
/// <param name="LineNumber">The line of the document where the breach is.</param>
public sealed record EcfSchemaViolation(string Message, int LineNumber);

/// <summary>
/// The official schema of one e-CF type, taken from the XSD the DGII publishes.
/// It answers two questions about a document of that type: what it may carry
/// and what it must carry.
/// </summary>
public sealed class EcfSchema
{
    private readonly XmlSchemaSet _schemas;
    private readonly SchemaNode _root;

    private EcfSchema(EcfDocumentType documentType, XmlSchemaSet schemas, SchemaNode root)
    {
        DocumentType = documentType;
        _schemas = schemas;
        _root = root;
    }

    /// <summary>The type of document this schema describes.</summary>
    public EcfDocumentType DocumentType { get; }

    /// <summary>
    /// Whether the given path, such as "Encabezado/IdDoc/eNCF", belongs to the
    /// format of this type.
    /// </summary>
    public bool Allows(string path) => Find(path) is not null;

    /// <summary>
    /// Whether the given path has to be present on a document of this type.
    /// </summary>
    public bool IsRequired(string path) => Find(path)?.Required is true;

    /// <summary>
    /// The values the field at the given path accepts, as the schema of this
    /// type enumerates them. It is empty when the field is not part of this
    /// type or when its values are not enumerated.
    /// </summary>
    public IReadOnlyList<string> AllowedValues(string path) => Find(path)?.AllowedValues ?? [];

    /// <summary>
    /// The highest number of times the given path may appear on a document of
    /// this type.
    /// </summary>
    public int MaxOccurs(string path) => Find(path)?.MaxOccurs ?? 0;

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
    /// Validates a document that has not been signed yet. The schema demands an
    /// element in the slot the digital signature goes into, so a placeholder
    /// stands in for the signature while the content of the document is
    /// checked.
    /// </summary>
    public IReadOnlyList<EcfSchemaViolation> ValidateUnsigned(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        var end = xml.LastIndexOf("</ECF>", StringComparison.Ordinal);
        if (end < 0)
            return Validate(xml);

        return Validate(xml.Insert(end, "<FirmaReservada />"));
    }

    private SchemaNode? Find(string path)
    {
        var current = _root;
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Children.TryGetValue(segment, out var child))
                current = child;
            else
                return null;
        }

        return current;
    }

    internal static EcfSchema Load(EcfDocumentType documentType, string xsdResource)
    {
        var assembly = typeof(EcfSchema).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(xsdResource, StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The schema resource '{xsdResource}' is missing.");

        var schema = ReadSchema(stream, xsdResource);
        var schemas = new XmlSchemaSet { XmlResolver = null };
        schemas.Add(schema);
        schemas.Compile();

        var rootElement = schemas.GlobalElements[new XmlQualifiedName("ECF")] as XmlSchemaElement
            ?? throw new InvalidOperationException($"The schema '{xsdResource}' has no ECF element.");

        return new EcfSchema(documentType, schemas, Build(rootElement, schemas));
    }

    private static XmlSchema ReadSchema(Stream stream, string xsdResource)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { XmlResolver = null });

        return XmlSchema.Read(reader, (_, args) =>
        {
            if (args.Severity == XmlSeverityType.Error)
                throw new InvalidOperationException($"The schema '{xsdResource}' is not valid: {args.Message}");
        }) ?? throw new InvalidOperationException($"The schema '{xsdResource}' could not be read.");
    }

    private static SchemaNode Build(XmlSchemaElement element, XmlSchemaSet schemas)
    {
        var children = new Dictionary<string, SchemaNode>(StringComparer.Ordinal);

        var complexType = element.SchemaType as XmlSchemaComplexType;
        if (complexType is null && element.SchemaTypeName is not null
            && schemas.GlobalTypes.Contains(element.SchemaTypeName))
        {
            complexType = schemas.GlobalTypes[element.SchemaTypeName] as XmlSchemaComplexType;
        }

        switch (complexType?.ContentTypeParticle)
        {
            case XmlSchemaSequence sequence:
                Collect(sequence.Items, schemas, children);
                break;
            case XmlSchemaChoice choice:
                Collect(choice.Items, schemas, children);
                break;
        }

        var maxOccurs = element.MaxOccurs == decimal.MaxValue ? -1 : (int)element.MaxOccurs;
        return new SchemaNode(element.Name!, element.MinOccurs >= 1, maxOccurs, children, EnumerationsOf(element, schemas));
    }

    private static IReadOnlyList<string> EnumerationsOf(XmlSchemaElement element, XmlSchemaSet schemas)
    {
        var simpleType = element.SchemaType as XmlSchemaSimpleType;

        if (simpleType is null && element.SchemaTypeName is not null
            && schemas.GlobalTypes.Contains(element.SchemaTypeName))
        {
            simpleType = schemas.GlobalTypes[element.SchemaTypeName] as XmlSchemaSimpleType;
        }

        if (simpleType?.Content is not XmlSchemaSimpleTypeRestriction restriction)
            return [];

        return restriction.Facets
            .OfType<XmlSchemaEnumerationFacet>()
            .Select(facet => facet.Value ?? string.Empty)
            .ToList();
    }

    private static void Collect(
        System.Collections.IEnumerable items,
        XmlSchemaSet schemas,
        Dictionary<string, SchemaNode> children)
    {
        foreach (var item in items.OfType<XmlSchemaElement>())
        {
            if (item.Name is not null)
                children[item.Name] = Build(item, schemas);
        }
    }

    private sealed class SchemaNode(
        string name,
        bool required,
        int maxOccurs,
        Dictionary<string, SchemaNode> children,
        IReadOnlyList<string> allowedValues)
    {
        public string Name { get; } = name;

        public bool Required { get; } = required;

        public int MaxOccurs { get; } = maxOccurs;

        public Dictionary<string, SchemaNode> Children { get; } = children;

        public IReadOnlyList<string> AllowedValues { get; } = allowedValues;
    }
}

/// <summary>
/// The schemas of the ten e-CF types, taken from the XSD files the DGII
/// publishes and shipped with this library.
/// </summary>
public static class EcfSchemas
{
    private static readonly ConcurrentDictionary<EcfDocumentType, EcfSchema> Schemas = new();

    /// <summary>
    /// The schema of the given type, loaded once and kept.
    /// </summary>
    public static EcfSchema For(EcfDocumentType documentType) =>
        Schemas.GetOrAdd(documentType, type => EcfSchema.Load(type, $"ecf-{(int)type}.xsd"));

    /// <summary>
    /// The names of the schema resources this library ships, for diagnostics.
    /// </summary>
    public static IReadOnlyList<string> ResourceNames() =>
        typeof(EcfSchemas).Assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Schemas.", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
}
