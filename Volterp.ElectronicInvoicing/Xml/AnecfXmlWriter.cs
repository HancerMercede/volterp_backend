using System.Globalization;
using System.Text;
using System.Xml;
using Volterp.ElectronicInvoicing.Schemas;

namespace Volterp.ElectronicInvoicing.Xml;

// Writes the annulment of the ranges of e-NCF as the XML the DGII expects,
// with the areas and the order of the "Formato Anulación de Secuencias de
// e-NCF v1.0".
//
// The output carries no indentation on purpose: the whitespace is part of what
// gets digested when the document is signed, and the DGII discards it before
// digesting.
public sealed class AnecfXmlWriter
{
    public const string FormatVersion = "1.0";

    private readonly AnecfSchema _schema;

    public AnecfXmlWriter() : this(AnecfSchema.Instance)
    {
    }

    public AnecfXmlWriter(AnecfSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        _schema = schema;
    }

    /// <summary>
    /// Writes the annulment of the ranges as the XML the DGII expects.
    /// </summary>
    /// <exception cref="EcfXmlException">
    /// The ranges do not belong to the type of their line, one of them ends
    /// before it starts, or the document does not match the official schema.
    /// </exception>
    public string Write(AnecfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Annulments.Count == 0)
            throw new EcfXmlException("The annulment of the ranges needs at least one line.");

        var counts = document.Annulments
            .Select(annulment => CountOf(annulment))
            .ToList();

        var settings = new XmlWriterSettings
        {
            Indent = false,
            OmitXmlDeclaration = false,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            NewLineHandling = NewLineHandling.None
        };

        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            using (var writer = XmlWriter.Create(stream, settings))
            {
                writer.WriteStartElement("ANECF");
                WriteHeader(writer, document, counts.Sum());
                WriteDetail(writer, document, counts);
                writer.WriteEndElement();
            }

            bytes = stream.ToArray();
        }

        // The document travels as UTF-8 bytes, so the declaration has to say so.
        var xml = new UTF8Encoding(false).GetString(bytes);

        var violations = _schema.ValidateUnsigned(xml);
        if (violations.Count > 0)
        {
            var details = string.Join(" | ", violations.Take(3).Select(violation => violation.Message));
            throw new EcfXmlException(
                $"The annulment of the ranges does not match the official schema: {details}",
                violations);
        }

        return xml;
    }

    private static void WriteHeader(XmlWriter writer, AnecfDocument document, int annulledCount)
    {
        writer.WriteStartElement("Encabezado");
        writer.WriteElementString("Version", FormatVersion);
        writer.WriteElementString("RncEmisor", document.IssuerTaxId);
        writer.WriteElementString("CantidadeNCFAnulados", Number(annulledCount));
        writer.WriteElementString(
            "FechaHoraAnulacioneNCF",
            document.GeneratedAt.ToString("dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }

    private static void WriteDetail(
        XmlWriter writer,
        AnecfDocument document,
        IReadOnlyList<int> counts)
    {
        writer.WriteStartElement("DetalleAnulacion");

        for (var index = 0; index < document.Annulments.Count; index++)
        {
            var annulment = document.Annulments[index];

            writer.WriteStartElement("Anulacion");
            writer.WriteElementString("NoLinea", Number(index + 1));
            writer.WriteElementString("TipoeCF", Number((int)annulment.DocumentType));

            writer.WriteStartElement("TablaRangoSecuenciasAnuladaseNCF");
            foreach (var range in annulment.Sequences)
            {
                writer.WriteStartElement("Secuencias");
                writer.WriteElementString("SecuenciaeNCFDesde", range.From);
                writer.WriteElementString("SecuenciaeNCFHasta", range.To);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteElementString("CantidadeNCFAnulados", Number(counts[index]));
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    // The count of the sequences of a line: the format asks for the amount of
    // e-NCF that is being annulled, and the ranges say how many they are.
    private static int CountOf(AnecfAnnulment annulment)
    {
        if (annulment.Sequences.Count == 0)
            throw new EcfXmlException("A line of the annulment needs at least one range of e-NCF.");

        return annulment.Sequences.Sum(range => CountOf(range, annulment.DocumentType));
    }

    private static int CountOf(AnecfSequenceRange range, EcfDocumentType documentType)
    {
        var (fromType, fromSequence) = Parse(range.From);
        var (toType, toSequence) = Parse(range.To);

        if (fromType != (int)documentType || toType != (int)documentType)
            throw new EcfXmlException(
                $"The e-NCF of the range does not belong to the type {(int)documentType} of the line.");

        if (fromSequence > toSequence)
            throw new EcfXmlException(
                $"The field SecuenciaeNCFDesde ({range.From}) is greater than the field SecuenciaeNCFHasta ({range.To}).");

        return toSequence - fromSequence + 1;
    }

    // The e-NCF of the format is a serie letter, the two digits of the type
    // and the ten of the sequence ("E310000000001").
    private static (int Type, int Sequence) Parse(string encf)
    {
        if (encf.Length != 13)
            throw new EcfXmlException($"'{encf}' is not an e-NCF of thirteen characters.");

        if (!int.TryParse(encf.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var type)
            || !int.TryParse(encf.AsSpan(3, 10), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence))
            throw new EcfXmlException($"'{encf}' does not carry the type and the sequence of an e-NCF.");

        return (type, sequence);
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
