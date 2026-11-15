using System.Globalization;
using System.Text;
using System.Xml;

namespace Volterp.ElectronicInvoicing.Xml;

// Writes the summary of a consumption invoice as the XML the DGII expects,
// with the areas and the order of the "Formato de Resumen Factura de Consumo
// Electrónica <DOP250,000".
//
// The output carries no indentation on purpose: the whitespace is part of what
// gets digested when the document is signed, and the DGII discards it before
// digesting.
public sealed class RfceXmlWriter
{
    public const string FormatVersion = "1.0";

    // The summary is always of a consumption invoice.
    private const int ConsumerDocumentType = 32;

    public string Write(RfceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(document.SecurityCode))
            throw new EcfXmlException("<CodigoSeguridadeCF> is required in the summary of a consumption invoice.");

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
                writer.WriteStartElement("RFCE");
                writer.WriteStartElement("Encabezado");
                writer.WriteElementString("Version", FormatVersion);
                WriteIdentity(writer, document.Identity);
                WriteIssuer(writer, document.Issuer);
                WriteBuyer(writer, document.Buyer);
                WriteTotals(writer, document.Totals);
                writer.WriteElementString("CodigoSeguridadeCF", document.SecurityCode);
                writer.WriteEndElement();
                writer.WriteEndElement();
            }

            bytes = stream.ToArray();
        }

        return new UTF8Encoding(false).GetString(bytes);
    }

    private static void WriteIdentity(XmlWriter writer, RfceIdentity identity)
    {
        writer.WriteStartElement("IdDoc");
        writer.WriteElementString("TipoeCF", ConsumerDocumentType.ToString(CultureInfo.InvariantCulture));
        writer.WriteElementString("eNCF", identity.ENCF);
        WriteElement(writer, "TipoIngresos", identity.TypeIncome is null ? null : EcfTypeIncomes.Code(identity.TypeIncome.Value));
        WriteElement(writer, "TipoPago", identity.PaymentType is null ? null : ((int)identity.PaymentType.Value).ToString(CultureInfo.InvariantCulture));

        if (identity.Payments.Count > 0)
        {
            writer.WriteStartElement("TablaFormasPago");
            foreach (var payment in identity.Payments)
            {
                writer.WriteStartElement("FormaDePago");
                writer.WriteElementString("FormaPago", ((int)payment.Method).ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("MontoPago", FormatMoney(payment.Amount));
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteIssuer(XmlWriter writer, RfceIssuer issuer)
    {
        writer.WriteStartElement("Emisor");
        writer.WriteElementString("RNCEmisor", issuer.TaxId);
        writer.WriteElementString("RazonSocialEmisor", issuer.BusinessName);
        writer.WriteElementString("FechaEmision", issuer.IssuedAt.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }

    private static void WriteBuyer(XmlWriter writer, RfceBuyer? buyer)
    {
        if (buyer is null)
            return;

        if (string.IsNullOrWhiteSpace(buyer.TaxId) && string.IsNullOrWhiteSpace(buyer.ForeignId))
            throw new EcfXmlException("The buyer of the summary needs an RNC or a foreign id.");

        writer.WriteStartElement("Comprador");

        // The format says so: a foreign buyer leaves the RNC of the buyer in
        // blank.
        if (string.IsNullOrWhiteSpace(buyer.ForeignId))
            WriteElement(writer, "RNCComprador", buyer.TaxId);

        WriteElement(writer, "IdentificadorExtranjero", buyer.ForeignId);
        WriteElement(writer, "RazonSocialComprador", buyer.BusinessName);

        writer.WriteEndElement();
    }

    private static void WriteTotals(XmlWriter writer, RfceTotals totals)
    {
        writer.WriteStartElement("Totales");
        WriteElement(writer, "MontoGravadoTotal", FormatMoney(totals.TaxableTotal));
        WriteElement(writer, "MontoGravadoI1", FormatMoney(totals.TaxableAtRate1));
        WriteElement(writer, "MontoGravadoI2", FormatMoney(totals.TaxableAtRate2));
        WriteElement(writer, "MontoGravadoI3", FormatMoney(totals.TaxableAtRate3));
        WriteElement(writer, "MontoExento", FormatMoney(totals.ExemptTotal));
        WriteElement(writer, "TotalITBIS", FormatMoney(totals.ItbisTotal));
        WriteElement(writer, "TotalITBIS1", FormatMoney(totals.ItbisAtRate1));
        WriteElement(writer, "TotalITBIS2", FormatMoney(totals.ItbisAtRate2));
        WriteElement(writer, "TotalITBIS3", FormatMoney(totals.ItbisAtRate3));
        WriteElement(writer, "MontoImpuestoAdicional", FormatMoney(totals.AdditionalTaxTotal));

        if (totals.AdditionalTaxes.Count > 0)
        {
            writer.WriteStartElement("ImpuestosAdicionales");
            foreach (var tax in totals.AdditionalTaxes)
            {
                writer.WriteStartElement("ImpuestoAdicional");
                writer.WriteElementString("TipoImpuesto", tax.Code.ToString(CultureInfo.InvariantCulture));
                WriteElement(writer, "MontoImpuestoSelectivoConsumoEspecifico", FormatMoney(tax.SpecificConsumptionAmount));
                WriteElement(writer, "MontoImpuestoSelectivoConsumoAdvalorem", FormatMoney(tax.AdValoremAmount));
                WriteElement(writer, "OtrosImpuestosAdicionales", FormatMoney(tax.OtherAmount));
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        WriteElement(writer, "MontoTotal", FormatMoney(totals.Total));
        WriteElement(writer, "MontoNoFacturable", FormatMoney(totals.NonBillableTotal));
        WriteElement(writer, "MontoPeriodo", FormatMoney(totals.PeriodTotal));
        writer.WriteEndElement();
    }

    private static void WriteElement(XmlWriter writer, string name, string? value)
    {
        if (value is null)
            return;

        writer.WriteElementString(name, value);
    }

    // An amount that does not apply is left out: the format makes most of
    // the totals conditional, and a zero is not the same as an absent one.
    private static string? FormatMoney(decimal? value) => value?.ToString("0.00", CultureInfo.InvariantCulture);
}
