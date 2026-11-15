using System.Globalization;
using System.Xml.Linq;

namespace Volterp.DgiiSimulator;

/// <summary>
/// The data the DGII takes out of a received e-CF to answer the consultations.
/// The tags are looked up by name wherever they sit, because the simulator
/// reads the document and does not validate it against the schema: that is the
/// DGII's verdict, not the simulator's.
/// </summary>
internal static class EcfDocumentReader
{
    public static EcfData Read(string xml)
    {
        var document = XDocument.Parse(xml);

        return new EcfData(
            IssuerRnc: Value(document, "RNCEmisor"),
            Encf: Value(document, "eNCF") ?? Value(document, "NcfElectronico"),
            BuyerRnc: Value(document, "RNCComprador"),
            ForeignId: Value(document, "IdentificadorExtranjero"),
            TotalAmount: Amount(document, "MontoTotal"),
            TotalItbis: Amount(document, "TotalITBIS"),
            IssueDate: Value(document, "FechaEmision"),
            SignedAt: Value(document, "FechaFirma"));
    }

    private static string? Value(XDocument document, string name) =>
        document.Descendants().FirstOrDefault(element => element.Name.LocalName == name)?.Value;

    private static decimal? Amount(XDocument document, string name)
    {
        var value = Value(document, name);
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}

internal sealed record EcfData(
    string? IssuerRnc,
    string? Encf,
    string? BuyerRnc,
    string? ForeignId,
    decimal? TotalAmount,
    decimal? TotalItbis,
    string? IssueDate,
    string? SignedAt);
