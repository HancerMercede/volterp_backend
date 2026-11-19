using System.Globalization;
using System.Text;
using System.Xml;
using Volterp.ElectronicInvoicing.Schemas;
using Volterp.ElectronicInvoicing.Validation;
namespace Volterp.ElectronicInvoicing.Xml;

/// <summary>
/// Writes a document as the XML the DGII expects.
///
/// The type of the document selects the schema that applies, so one model
/// produces a different document depending on the type: the writer emits only
/// what that type accepts, and it refuses to write a document that misses what
/// that type requires.
///
/// The output carries no indentation on purpose. Whitespace is part of what
/// gets digested when the document is signed, and the DGII discards it before
/// digesting, so a document with whitespace would not match its own
/// signature.
/// </summary>
public sealed class EcfXmlWriter
{
    /// <summary>The version of the format the DGII publishes.</summary>
    public const string FormatVersion = "1.0";

    private const string TypeIncomePath = "Encabezado/IdDoc/TipoIngresos";

    private readonly Func<EcfDocumentType, EcfSchema> _schemaFor;
    private readonly bool _validateOutput;

    /// <summary>
    /// Creates a writer that uses the schemas shipped with this library and
    /// checks the result against them.
    /// </summary>
    public EcfXmlWriter() : this(EcfSchemas.For, validateOutput: true)
    {
    }

    /// <summary>
    /// Creates a writer with the given source of schemas.
    /// </summary>
    /// <param name="schemaFor">Returns the schema of a document type.</param>
    /// <param name="validateOutput">Whether to check the written document against its schema.</param>
    public EcfXmlWriter(Func<EcfDocumentType, EcfSchema> schemaFor, bool validateOutput = true)
    {
        ArgumentNullException.ThrowIfNull(schemaFor);
        _schemaFor = schemaFor;
        _validateOutput = validateOutput;
    }

    /// <summary>
    /// Writes the document as the XML the DGII expects.
    /// </summary>
    /// <exception cref="EcfXmlException">
    /// The document misses something its type requires, its totals do not add
    /// up to its lines, or it does not match the official schema.
    /// </exception>
    public string Write(EcfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        // The rules of the type and the arithmetic of the document are checked
        // before anything is written: a document that does not add up is not
        // written, and therefore never signed.
        var breaches = EcfDocumentValidator.Validate(document);

        if (breaches.Count > 0)
        {
            var details = string.Join(" | ", breaches.Take(3).Select(breach => breach.Message));
            var more = breaches.Count > 3 ? $" (and {breaches.Count - 3} more)" : string.Empty;

            throw new EcfXmlException(
                $"The e-CF {(int)document.DocumentType} is not valid: {details}{more}");
        }

        var schema = _schemaFor(document.DocumentType);
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
                writer.WriteStartElement("ECF");
                WriteHeader(writer, schema, document);
                WriteItems(writer, schema, document);
                WriteAdjustments(writer, schema, document);
                WriteReference(writer, schema, document);
                WriteElement(writer, schema, "FechaHoraFirma", FormatDateTime(document.SignedAt));
                writer.WriteEndElement();
            }

            bytes = stream.ToArray();
        }

        // The document travels as UTF-8 bytes, so the declaration has to say so.
        var xml = new UTF8Encoding(false).GetString(bytes);

        if (_validateOutput)
        {
            var violations = schema.ValidateUnsigned(xml);
            if (violations.Count > 0)
            {
                var details = string.Join(" | ", violations.Take(3).Select(violation => violation.Message));
                throw new EcfXmlException(
                    $"The e-CF {(int)document.DocumentType} does not match the official schema: {details}",
                    violations);
            }
        }

        return xml;
    }

    private void WriteHeader(XmlWriter writer, EcfSchema schema, EcfDocument document)
    {
        writer.WriteStartElement("Encabezado");
        WriteElement(writer, schema, "Encabezado/Version", FormatVersion);
        WriteIdentity(writer, schema, document);
        WriteIssuer(writer, schema, document);
        WriteBuyer(writer, schema, document);
        WriteTotals(writer, schema, document);
        writer.WriteEndElement();
    }

    private void WriteIdentity(XmlWriter writer, EcfSchema schema, EcfDocument document)
    {
        var identity = document.Identity;

        writer.WriteStartElement("IdDoc");
        WriteElement(writer, schema, "Encabezado/IdDoc/TipoeCF", Code((int)document.DocumentType));
        WriteElement(writer, schema, "Encabezado/IdDoc/eNCF", identity.ENCF);
        WriteElement(writer, schema, "Encabezado/IdDoc/IndicadorNotaCredito", Flag(identity.CreditNoteBeyondThirtyDays));
        WriteElement(writer, schema, "Encabezado/IdDoc/FechaVencimientoSecuencia", FormatDate(identity.SequenceExpiresAt));
        WriteElement(writer, schema, "Encabezado/IdDoc/IndicadorMontoGravado", Flag(identity.LineAmountsIncludeItbis));
        WriteElement(writer, schema, "Encabezado/IdDoc/TipoIngresos", TypeIncomeCode(document, schema));
        WriteElement(writer, schema, "Encabezado/IdDoc/TipoPago", identity.PaymentType is null ? null : Code((int)identity.PaymentType.Value));
        WriteElement(writer, schema, "Encabezado/IdDoc/FechaLimitePago", FormatDate(identity.PaymentDueDate));
        WriteElement(writer, schema, "Encabezado/IdDoc/TerminoPago", identity.PaymentTerm);
        WritePayments(writer, schema, identity);
        WritePaymentAccount(writer, schema, identity);
        writer.WriteEndElement();
    }

    private void WritePayments(XmlWriter writer, EcfSchema schema, EcfIdentity identity)
    {
        if (identity.Payments.Count == 0 || !schema.Allows("Encabezado/IdDoc/TablaFormasPago"))
            return;

        writer.WriteStartElement("TablaFormasPago");
        foreach (var payment in identity.Payments)
        {
            writer.WriteStartElement("FormaDePago");
            WriteElement(writer, schema, "Encabezado/IdDoc/TablaFormasPago/FormaDePago/FormaPago", Code((int)payment.Method));
            WriteElement(writer, schema, "Encabezado/IdDoc/TablaFormasPago/FormaDePago/MontoPago", FormatMoney(payment.Amount));
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private void WritePaymentAccount(XmlWriter writer, EcfSchema schema, EcfIdentity identity)
    {
        var account = identity.PaymentAccount;
        if (account is null)
            return;

        WriteElement(writer, schema, "Encabezado/IdDoc/TipoCuentaPago", account.Type switch
        {
            EcfAccountType.Current => "CT",
            EcfAccountType.Savings => "AH",
            _ => "OT"
        });
        WriteElement(writer, schema, "Encabezado/IdDoc/NumeroCuentaPago", account.Number);
        WriteElement(writer, schema, "Encabezado/IdDoc/BancoPago", account.Bank);
    }

    private void WriteIssuer(XmlWriter writer, EcfSchema schema, EcfDocument document)
    {
        var issuer = document.Issuer;

        writer.WriteStartElement("Emisor");
        WriteElement(writer, schema, "Encabezado/Emisor/RNCEmisor", issuer.TaxId);
        WriteElement(writer, schema, "Encabezado/Emisor/RazonSocialEmisor", issuer.BusinessName);
        WriteElement(writer, schema, "Encabezado/Emisor/NombreComercial", issuer.TradeName);
        WriteElement(writer, schema, "Encabezado/Emisor/Sucursal", issuer.Branch);
        WriteElement(writer, schema, "Encabezado/Emisor/DireccionEmisor", issuer.Address);
        WriteElement(writer, schema, "Encabezado/Emisor/Municipio", issuer.Municipality);
        WriteElement(writer, schema, "Encabezado/Emisor/Provincia", issuer.Province);
        WritePhoneNumbers(writer, schema, issuer);
        WriteElement(writer, schema, "Encabezado/Emisor/CorreoEmisor", issuer.Email);
        WriteElement(writer, schema, "Encabezado/Emisor/WebSite", issuer.Website);
        WriteElement(writer, schema, "Encabezado/Emisor/ActividadEconomica", issuer.EconomicActivity);
        WriteElement(writer, schema, "Encabezado/Emisor/CodigoVendedor", issuer.SellerCode);
        WriteElement(writer, schema, "Encabezado/Emisor/NumeroFacturaInterna", issuer.InternalInvoiceNumber);
        WriteElement(writer, schema, "Encabezado/Emisor/NumeroPedidoInterno", issuer.InternalOrderNumber);
        WriteElement(writer, schema, "Encabezado/Emisor/ZonaVenta", issuer.SalesZone);
        WriteElement(writer, schema, "Encabezado/Emisor/RutaVenta", issuer.SalesRoute);
        WriteElement(writer, schema, "Encabezado/Emisor/InformacionAdicionalEmisor", issuer.AdditionalInformation);
        WriteElement(writer, schema, "Encabezado/Emisor/FechaEmision", FormatDate(issuer.IssuedAt));
        writer.WriteEndElement();
    }

    private void WritePhoneNumbers(XmlWriter writer, EcfSchema schema, EcfIssuer issuer)
    {
        if (issuer.PhoneNumbers.Count == 0 || !schema.Allows("Encabezado/Emisor/TablaTelefonoEmisor"))
            return;

        writer.WriteStartElement("TablaTelefonoEmisor");
        foreach (var phone in issuer.PhoneNumbers)
        {
            WriteElement(writer, schema, "Encabezado/Emisor/TablaTelefonoEmisor/TelefonoEmisor", phone);
        }
        writer.WriteEndElement();
    }

    private void WriteBuyer(XmlWriter writer, EcfSchema schema, EcfDocument document)
    {
        var buyer = document.Buyer;

        if (buyer is null)
        {
            if (schema.IsRequired("Encabezado/Comprador"))
                throw new EcfXmlException($"<Comprador> is required for e-CF {(int)schema.DocumentType}.");
            return;
        }

        if (!schema.Allows("Encabezado/Comprador"))
            return;

        writer.WriteStartElement("Comprador");
        WriteElement(writer, schema, "Encabezado/Comprador/RNCComprador", buyer.TaxId);
        WriteElement(writer, schema, "Encabezado/Comprador/RazonSocialComprador", buyer.BusinessName);
        WriteElement(writer, schema, "Encabezado/Comprador/ContactoComprador", buyer.Contact);
        WriteElement(writer, schema, "Encabezado/Comprador/CorreoComprador", buyer.Email);
        WriteElement(writer, schema, "Encabezado/Comprador/DireccionComprador", buyer.Address);
        WriteElement(writer, schema, "Encabezado/Comprador/MunicipioComprador", buyer.Municipality);
        WriteElement(writer, schema, "Encabezado/Comprador/ProvinciaComprador", buyer.Province);
        WriteElement(writer, schema, "Encabezado/Comprador/FechaEntrega", FormatDate(buyer.DeliveredAt));
        WriteElement(writer, schema, "Encabezado/Comprador/ContactoEntrega", buyer.DeliveryContact);
        WriteElement(writer, schema, "Encabezado/Comprador/DireccionEntrega", buyer.DeliveryAddress);
        WriteElement(writer, schema, "Encabezado/Comprador/TelefonoAdicional", buyer.AdditionalPhone);
        writer.WriteEndElement();
    }

    private void WriteTotals(XmlWriter writer, EcfSchema schema, EcfDocument document)
    {
        var totals = document.Totals;

        writer.WriteStartElement("Totales");
        WriteElement(writer, schema, "Encabezado/Totales/MontoGravadoTotal", FormatMoney(totals.TaxableTotal));
        WriteElement(writer, schema, "Encabezado/Totales/MontoGravadoI1", FormatMoney(totals.TaxableAtRate1));
        WriteElement(writer, schema, "Encabezado/Totales/MontoGravadoI2", FormatMoney(totals.TaxableAtRate2));
        WriteElement(writer, schema, "Encabezado/Totales/MontoGravadoI3", FormatMoney(totals.TaxableAtRate3));
        WriteElement(writer, schema, "Encabezado/Totales/MontoExento", FormatMoney(totals.ExemptTotal));
        WriteElement(writer, schema, "Encabezado/Totales/ITBIS1", totals.ItbisRate1?.ToString(CultureInfo.InvariantCulture));
        WriteElement(writer, schema, "Encabezado/Totales/ITBIS2", totals.ItbisRate2?.ToString(CultureInfo.InvariantCulture));
        WriteElement(writer, schema, "Encabezado/Totales/ITBIS3", totals.ItbisRate3?.ToString(CultureInfo.InvariantCulture));
        WriteElement(writer, schema, "Encabezado/Totales/TotalITBIS", FormatMoney(totals.ItbisTotal));
        WriteElement(writer, schema, "Encabezado/Totales/TotalITBIS1", FormatMoney(totals.ItbisAtRate1));
        WriteElement(writer, schema, "Encabezado/Totales/TotalITBIS2", FormatMoney(totals.ItbisAtRate2));
        WriteElement(writer, schema, "Encabezado/Totales/TotalITBIS3", FormatMoney(totals.ItbisAtRate3));
        WriteElement(writer, schema, "Encabezado/Totales/MontoImpuestoAdicional", FormatMoney(totals.AdditionalTaxTotal));
        WriteElement(writer, schema, "Encabezado/Totales/MontoTotal", FormatMoney(totals.Total));
        WriteElement(writer, schema, "Encabezado/Totales/MontoNoFacturable", FormatMoney(totals.NonBillableTotal));
        WriteElement(writer, schema, "Encabezado/Totales/MontoPeriodo", FormatMoney(totals.PeriodTotal));
        WriteElement(writer, schema, "Encabezado/Totales/SaldoAnterior", FormatMoney(totals.PreviousBalance));
        WriteElement(writer, schema, "Encabezado/Totales/MontoAvancePago", FormatMoney(totals.PaymentAdvance));
        WriteElement(writer, schema, "Encabezado/Totales/ValorPagar", FormatMoney(totals.AmountToPay));
        WriteElement(writer, schema, "Encabezado/Totales/TotalITBISRetenido", FormatMoney(totals.ItbisWithheld));
        WriteElement(writer, schema, "Encabezado/Totales/TotalISRRetencion", FormatMoney(totals.IncomeTaxWithheld));
        WriteElement(writer, schema, "Encabezado/Totales/TotalITBISPercepcion", FormatMoney(totals.ItbisPerceived));
        WriteElement(writer, schema, "Encabezado/Totales/TotalISRPercepcion", FormatMoney(totals.IncomeTaxPerceived));
        writer.WriteEndElement();
    }

    private void WriteItems(XmlWriter writer, EcfSchema schema, EcfDocument document)
    {
        writer.WriteStartElement("DetallesItems");

        foreach (var item in document.Items)
        {
            writer.WriteStartElement("Item");
            WriteElement(writer, schema, "DetallesItems/Item/NumeroLinea", item.LineNumber.ToString(CultureInfo.InvariantCulture));
            WriteItemCodes(writer, schema, item);
            WriteElement(writer, schema, "DetallesItems/Item/IndicadorFacturacion", Code((int)item.TaxTreatment));
            WriteRetention(writer, schema, item);
            WriteElement(writer, schema, "DetallesItems/Item/NombreItem", item.Name);
            WriteElement(writer, schema, "DetallesItems/Item/IndicadorBienoServicio", Code((int)item.Kind));
            WriteElement(writer, schema, "DetallesItems/Item/DescripcionItem", item.Description);
            WriteElement(writer, schema, "DetallesItems/Item/CantidadItem", FormatQuantity(item.Quantity));
            WriteElement(writer, schema, "DetallesItems/Item/UnidadMedida", item.UnitOfMeasure?.ToString(CultureInfo.InvariantCulture));
            WriteElement(writer, schema, "DetallesItems/Item/PrecioUnitarioItem", FormatUnitPrice(item.UnitPrice));
            WriteElement(writer, schema, "DetallesItems/Item/DescuentoMonto", FormatMoney(item.DiscountAmount));
            WriteElement(writer, schema, "DetallesItems/Item/RecargoMonto", FormatMoney(item.SurchargeAmount));
            WriteElement(writer, schema, "DetallesItems/Item/MontoItem", FormatMoney(item.Amount));
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private void WriteItemCodes(XmlWriter writer, EcfSchema schema, EcfItem item)
    {
        if (item.Codes.Count == 0 || !schema.Allows("DetallesItems/Item/TablaCodigosItem"))
            return;

        writer.WriteStartElement("TablaCodigosItem");
        foreach (var code in item.Codes)
        {
            writer.WriteStartElement("CodigosItem");
            WriteElement(writer, schema, "DetallesItems/Item/TablaCodigosItem/CodigosItem/TipoCodigo", code.Type);
            WriteElement(writer, schema, "DetallesItems/Item/TablaCodigosItem/CodigosItem/CodigoItem", code.Value);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private void WriteRetention(XmlWriter writer, EcfSchema schema, EcfItem item)
    {
        var retention = item.Retention;
        if (retention is null || !schema.Allows("DetallesItems/Item/Retencion"))
            return;

        writer.WriteStartElement("Retencion");
        WriteElement(writer, schema, "DetallesItems/Item/Retencion/IndicadorAgenteRetencionoPercepcion", Flag(retention.IsWithholdingAgent));
        WriteElement(writer, schema, "DetallesItems/Item/Retencion/MontoITBISRetenido", FormatMoney(retention.ItbisWithheld));
        WriteElement(writer, schema, "DetallesItems/Item/Retencion/MontoISRRetenido", FormatMoney(retention.IncomeTaxWithheld));
        writer.WriteEndElement();
    }

    private void WriteAdjustments(XmlWriter writer, EcfSchema schema, EcfDocument document)
    {
        if (document.Adjustments.Count == 0 || !schema.Allows("DescuentosORecargos"))
            return;

        writer.WriteStartElement("DescuentosORecargos");
        foreach (var adjustment in document.Adjustments)
        {
            writer.WriteStartElement("DescuentoORecargo");
            WriteElement(writer, schema, "DescuentosORecargos/DescuentoORecargo/NumeroLinea", adjustment.LineNumber.ToString(CultureInfo.InvariantCulture));
            WriteElement(writer, schema, "DescuentosORecargos/DescuentoORecargo/TipoAjuste", adjustment.Kind.ToString());
            WriteElement(writer, schema, "DescuentosORecargos/DescuentoORecargo/IndicadorNorma1007", Flag(adjustment.FollowsNorm1007));
            WriteElement(writer, schema, "DescuentosORecargos/DescuentoORecargo/DescripcionDescuentooRecargo", adjustment.Description);
            WriteElement(writer, schema, "DescuentosORecargos/DescuentoORecargo/TipoValor", adjustment.ValueKind?.ToString());
            WriteElement(writer, schema, "DescuentosORecargos/DescuentoORecargo/ValorDescuentooRecargo", FormatMoney(adjustment.Value));
            WriteElement(writer, schema, "DescuentosORecargos/DescuentoORecargo/MontoDescuentooRecargo", FormatMoney(adjustment.Amount));
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private void WriteReference(XmlWriter writer, EcfSchema schema, EcfDocument document)
    {
        var reference = document.Reference;
        if (reference is null)
        {
            if (schema.IsRequired("InformacionReferencia"))
                throw new EcfXmlException($"<InformacionReferencia> is required for e-CF {(int)schema.DocumentType}.");
            return;
        }

        if (!schema.Allows("InformacionReferencia"))
            return;

        writer.WriteStartElement("InformacionReferencia");
        WriteElement(writer, schema, "InformacionReferencia/NCFModificado", reference.ModifiedENCF);
        WriteElement(writer, schema, "InformacionReferencia/RNCOtroContribuyente", reference.OtherContributorTaxId);
        WriteElement(writer, schema, "InformacionReferencia/FechaNCFModificado", FormatDate(reference.ModifiedAt));
        WriteElement(writer, schema, "InformacionReferencia/CodigoModificacion", reference.ModificationCode is null ? null : Code((int)reference.ModificationCode.Value));
        WriteElement(writer, schema, "InformacionReferencia/RazonModificacion", reference.ModificationReason);
        writer.WriteEndElement();
    }

    // The income of a document has to be one the schema of its type accepts:
    // a type whose schema declares no income accepts none, and a type that
    // declares it accepts only the codes it enumerates.
    private static string? TypeIncomeCode(EcfDocument document, EcfSchema schema)
    {
        if (document.Identity.TypeIncome is null)
            return null;

        var code = EcfTypeIncomes.Code(document.Identity.TypeIncome.Value);

        if (!schema.Allows(TypeIncomePath))
            throw new EcfXmlException(
                $"<TipoIngresos> is not a field the schema of the e-CF {(int)schema.DocumentType} accepts.");

        var allowed = schema.AllowedValues(TypeIncomePath);

        if (allowed.Count > 0 && !allowed.Contains(code))
            throw new EcfXmlException(
                $"<TipoIngresos> {code} is not an income code the schema of the e-CF {(int)schema.DocumentType} accepts.");

        return code;
    }

    private static void WriteElement(XmlWriter writer, EcfSchema schema, string path, string? value)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];

        if (value is null)
        {
            if (schema.IsRequired(path))
                throw new EcfXmlException($"<{name}> is required for e-CF {(int)schema.DocumentType}.");
            return;
        }

        if (!schema.Allows(path))
            return;

        writer.WriteElementString(name, value);
    }

    private static string Code(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string? Flag(bool? value) => value is null ? null : value.Value ? "1" : "0";

    private static string? FormatMoney(decimal? value) => value?.ToString("0.00", CultureInfo.InvariantCulture);

    private static string? FormatQuantity(decimal? value) => value?.ToString("0.##", CultureInfo.InvariantCulture);

    private static string? FormatUnitPrice(decimal? value) => value?.ToString("0.00##", CultureInfo.InvariantCulture);

    private static string? FormatDate(DateTime? value) => value?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    private static string? FormatDateTime(DateTime? value) => value?.ToString("dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
}
