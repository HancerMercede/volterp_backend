using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.ElectronicInvoicing.Printing;

// Reads a signed e-CF back into the model of the engine.
//
// The printed representation is built from what was signed: the amounts, the
// names and the address are the ones the DGII received, not the ones the
// ERP happens to hold today.
//
// The reading fails closed: a value the signed document carries and this
// reader cannot understand is not a value the representation may show.
public static class EcfSignedDocumentReader
{
    // The codes of the additional taxes ("CodificacionTipoImpuestosType" of
    // the official schemas).
    private const string LegalTipCode = "001";
    private const string TelecomDevelopmentCode = "002";

    public static EcfSignedDocument Read(string signedXml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signedXml);

        var document = new XmlDocument { PreserveWhitespace = false };
        document.LoadXml(signedXml);

        VerifySignature(document);

        var root = document.DocumentElement
            ?? throw new InvalidOperationException("The signed document is empty.");

        var ecf = new EcfDocument
        {
            DocumentType = DocumentTypeOf(root),
            Identity = IdentityOf(root),
            Issuer = IssuerOf(root),
            Buyer = BuyerOf(root),
            Totals = TotalsOf(root),
            Items = ItemsOf(root),
            Adjustments = AdjustmentsOf(root),
            Reference = ReferenceOf(root),
            SignedAt = SignedAtOf(root)
        };

        ValidateSequenceExpiry(ecf);
        ValidateReference(ecf);

        return new EcfSignedDocument
        {
            Document = ecf,
            AdditionalTaxes = AdditionalTaxesOf(
                root,
                ecf.Totals.AdditionalTaxTotal,
                ItemAdditionalTaxCodesOf(root)),
            Pagination = PaginationOf(root, ecf.Items),
            ItemAmountsIncludeItbis = Flag(root.SelectSingleNode("Encabezado/IdDoc/IndicadorMontoGravado")) == true
        };
    }

    private static EcfDocumentType DocumentTypeOf(XmlNode root)
    {
        var value = Text(root.SelectSingleNode("Encabezado/IdDoc/TipoeCF"))
            ?? throw new InvalidOperationException("The signed document carries no type of document.");

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var type)
            || !Enum.IsDefined((EcfDocumentType)type))
            throw new InvalidOperationException($"The signed document carries an unknown type of e-CF: {value}.");

        return (EcfDocumentType)type;
    }

    private static EcfIdentity IdentityOf(XmlNode root)
    {
        var income = Text(root.SelectSingleNode("Encabezado/IdDoc/TipoIngresos"));

        return new EcfIdentity
        {
            ENCF = Text(root.SelectSingleNode("Encabezado/IdDoc/eNCF"))
                ?? throw new InvalidOperationException("The signed document carries no e-NCF."),
            SequenceExpiresAt = Day(root.SelectSingleNode("Encabezado/IdDoc/FechaVencimientoSecuencia")),
            CreditNoteBeyondThirtyDays = Flag(root.SelectSingleNode("Encabezado/IdDoc/IndicadorNotaCredito")),
            LineAmountsIncludeItbis = Flag(root.SelectSingleNode("Encabezado/IdDoc/IndicadorMontoGravado")) ?? false,
            TypeIncome = income is null ? null : EcfTypeIncomes.FromCode(income),
            PaymentType = PaymentType(root.SelectSingleNode("Encabezado/IdDoc/TipoPago")),
            PaymentDueDate = Day(root.SelectSingleNode("Encabezado/IdDoc/FechaLimitePago")),
            PaymentTerm = Text(root.SelectSingleNode("Encabezado/IdDoc/TerminoPago"))
        };
    }

    private static EcfIssuer IssuerOf(XmlNode root)
    {
        var issuedAt = Day(root.SelectSingleNode("Encabezado/Emisor/FechaEmision"))
            ?? throw new InvalidOperationException("The signed document carries no date of issue.");

        return new EcfIssuer
        {
            TaxId = Text(root.SelectSingleNode("Encabezado/Emisor/RNCEmisor"))
                ?? throw new InvalidOperationException("The signed document carries no tax id of the issuer."),
            BusinessName = Text(root.SelectSingleNode("Encabezado/Emisor/RazonSocialEmisor"))
                ?? throw new InvalidOperationException("The signed document carries no name of the issuer."),
            TradeName = Text(root.SelectSingleNode("Encabezado/Emisor/NombreComercial")),
            Branch = Text(root.SelectSingleNode("Encabezado/Emisor/Sucursal")),
            Address = Text(root.SelectSingleNode("Encabezado/Emisor/DireccionEmisor"))
                ?? throw new InvalidOperationException("The signed document carries no address of the issuer."),
            Municipality = Text(root.SelectSingleNode("Encabezado/Emisor/Municipio")),
            Province = Text(root.SelectSingleNode("Encabezado/Emisor/Provincia")),
            PhoneNumbers = PhonesOf(root),
            Email = Text(root.SelectSingleNode("Encabezado/Emisor/CorreoEmisor")),
            Website = Text(root.SelectSingleNode("Encabezado/Emisor/WebSite")),
            EconomicActivity = Text(root.SelectSingleNode("Encabezado/Emisor/ActividadEconomica")),
            SellerCode = Text(root.SelectSingleNode("Encabezado/Emisor/CodigoVendedor")),
            InternalInvoiceNumber = Text(root.SelectSingleNode("Encabezado/Emisor/NumeroFacturaInterna")),
            InternalOrderNumber = Text(root.SelectSingleNode("Encabezado/Emisor/NumeroPedidoInterno")),
            SalesZone = Text(root.SelectSingleNode("Encabezado/Emisor/ZonaVenta")),
            SalesRoute = Text(root.SelectSingleNode("Encabezado/Emisor/RutaVenta")),
            AdditionalInformation = Text(root.SelectSingleNode("Encabezado/Emisor/InformacionAdicionalEmisor")),
            IssuedAt = issuedAt
        };
    }

    private static IReadOnlyList<string> PhonesOf(XmlNode root)
    {
        var nodes = root.SelectNodes("Encabezado/Emisor/TablaTelefonoEmisor/TelefonoEmisor");

        if (nodes is null)
            return [];

        return nodes
            .Cast<XmlNode>()
            .Select(Text)
            .Where(phone => phone is not null)
            .Select(phone => phone!)
            .ToList();
    }

    private static EcfBuyer? BuyerOf(XmlNode root)
    {
        var buyer = root.SelectSingleNode("Encabezado/Comprador");

        if (buyer is null)
            return null;

        return new EcfBuyer
        {
            TaxId = Text(buyer.SelectSingleNode("RNCComprador")),
            BusinessName = Text(buyer.SelectSingleNode("RazonSocialComprador")),
            Contact = Text(buyer.SelectSingleNode("ContactoComprador")),
            Email = Text(buyer.SelectSingleNode("CorreoComprador")),
            Address = Text(buyer.SelectSingleNode("DireccionComprador")),
            Municipality = Text(buyer.SelectSingleNode("MunicipioComprador")),
            Province = Text(buyer.SelectSingleNode("ProvinciaComprador")),
            DeliveredAt = Day(buyer.SelectSingleNode("FechaEntrega")),
            DeliveryContact = Text(buyer.SelectSingleNode("ContactoEntrega")),
            DeliveryAddress = Text(buyer.SelectSingleNode("DireccionEntrega")),
            AdditionalPhone = Text(buyer.SelectSingleNode("TelefonoAdicional"))
        };
    }

    private static EcfTotals TotalsOf(XmlNode root)
    {
        var total = Money(root.SelectSingleNode("Encabezado/Totales/MontoTotal"))
            ?? throw new InvalidOperationException("The signed document carries no total.");

        return new EcfTotals
        {
            TaxableTotal = Money(root.SelectSingleNode("Encabezado/Totales/MontoGravadoTotal")) ?? 0m,
            TaxableAtRate1 = Money(root.SelectSingleNode("Encabezado/Totales/MontoGravadoI1")),
            TaxableAtRate2 = Money(root.SelectSingleNode("Encabezado/Totales/MontoGravadoI2")),
            TaxableAtRate3 = Money(root.SelectSingleNode("Encabezado/Totales/MontoGravadoI3")),
            ExemptTotal = Money(root.SelectSingleNode("Encabezado/Totales/MontoExento")),
            ItbisRate1 = Integer(root.SelectSingleNode("Encabezado/Totales/ITBIS1")),
            ItbisRate2 = Integer(root.SelectSingleNode("Encabezado/Totales/ITBIS2")),
            ItbisRate3 = Integer(root.SelectSingleNode("Encabezado/Totales/ITBIS3")),
            ItbisTotal = Money(root.SelectSingleNode("Encabezado/Totales/TotalITBIS")) ?? 0m,
            ItbisAtRate1 = Money(root.SelectSingleNode("Encabezado/Totales/TotalITBIS1")),
            ItbisAtRate2 = Money(root.SelectSingleNode("Encabezado/Totales/TotalITBIS2")),
            ItbisAtRate3 = Money(root.SelectSingleNode("Encabezado/Totales/TotalITBIS3")),
            AdditionalTaxTotal = Money(root.SelectSingleNode("Encabezado/Totales/MontoImpuestoAdicional")),
            Total = total,
            NonBillableTotal = Money(root.SelectSingleNode("Encabezado/Totales/MontoNoFacturable")),
            PeriodTotal = Money(root.SelectSingleNode("Encabezado/Totales/MontoPeriodo")),
            PreviousBalance = Money(root.SelectSingleNode("Encabezado/Totales/SaldoAnterior")),
            PaymentAdvance = Money(root.SelectSingleNode("Encabezado/Totales/MontoAvancePago")),
            AmountToPay = Money(root.SelectSingleNode("Encabezado/Totales/ValorPagar")),
            ItbisWithheld = Money(root.SelectSingleNode("Encabezado/Totales/TotalITBISRetenido")),
            IncomeTaxWithheld = Money(root.SelectSingleNode("Encabezado/Totales/TotalISRRetencion")),
            ItbisPerceived = Money(root.SelectSingleNode("Encabezado/Totales/TotalITBISPercepcion")),
            IncomeTaxPerceived = Money(root.SelectSingleNode("Encabezado/Totales/TotalISRPercepcion"))
        };
    }

    // The additional taxes: the selective consumption tax comes in its own
    // fields and the legal tip and the contribution to the development of the
    // telecommunications come as the other additional taxes of the entry
    // whose code names them.
    private static IReadOnlyDictionary<int, IReadOnlyList<string>> ItemAdditionalTaxCodesOf(XmlNode root)
    {
        var nodes = root.SelectNodes("DetallesItems/Item");
        var result = new Dictionary<int, List<string>>();

        if (nodes is null)
            return result.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value.AsReadOnly());

        foreach (XmlNode node in nodes)
        {
            var line = Integer(node.SelectSingleNode("NumeroLinea"))
                ?? throw new InvalidOperationException("The signed document carries a line without a number.");
            var codes = new List<string>();
            var codeNodes = node.SelectNodes("TablaImpuestoAdicional/ImpuestoAdicional/TipoImpuesto");

            if (codeNodes is not null)
            {
                foreach (XmlNode codeNode in codeNodes)
                {
                    var code = AdditionalTaxCodeOf(codeNode);

                    if (!codes.Contains(code))
                        codes.Add(code);
                }
            }

            if (!result.TryAdd(line, codes))
                throw new InvalidOperationException("The signed document carries the same line more than once.");
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.AsReadOnly());
    }

    private static EcfPrintAdditionalTaxes AdditionalTaxesOf(
        XmlNode root,
        decimal? additionalTotal,
        IReadOnlyDictionary<int, IReadOnlyList<string>> itemTaxCodes)
    {
        var nodes = root.SelectNodes("Encabezado/Totales/ImpuestosAdicionales/ImpuestoAdicional");
        var declaredSelectiveCodes = itemTaxCodes.Values
            .SelectMany(codes => codes)
            .Where(IsSelectiveConsumptionTaxCode)
            .Distinct()
            .ToList();

        if (nodes is null || nodes.Count == 0)
        {
            if (declaredSelectiveCodes.Count > 0)
                throw new InvalidOperationException("A line declares a selective consumption tax that the signed totals do not carry.");

            return new EcfPrintAdditionalTaxes { SelectiveConsumptionTax = additionalTotal };
        }

        decimal? isc = null;
        decimal? cdt = null;
        decimal? tip = null;
        var entries = new List<EcfPrintAdditionalTaxEntry>();

        foreach (XmlNode node in nodes)
        {
            var code = AdditionalTaxCodeOf(node.SelectSingleNode("TipoImpuesto"));
            var specific = Money(node.SelectSingleNode("MontoImpuestoSelectivoConsumoEspecifico"));
            var advalorem = Money(node.SelectSingleNode("MontoImpuestoSelectivoConsumoAdvalorem"));
            var others = Money(node.SelectSingleNode("OtrosImpuestosAdicionales"));
            var selective = (specific ?? 0m) + (advalorem ?? 0m);

            if (selective != 0m)
                isc = (isc ?? 0m) + selective;

            if (others is not null and not 0m)
            {
                switch (code)
                {
                    case LegalTipCode:
                        tip = (tip ?? 0m) + others.Value;
                        break;
                    case TelecomDevelopmentCode:
                        cdt = (cdt ?? 0m) + others.Value;
                        break;
                }
            }

            entries.Add(new EcfPrintAdditionalTaxEntry
            {
                Code = code,
                SelectiveConsumptionTax = specific,
                AdValoremSelectiveConsumptionTax = advalorem,
                OtherTax = others
            });
        }

        foreach (var code in declaredSelectiveCodes)
        {
            var applicableLines = itemTaxCodes
                .Where(pair => pair.Value.Contains(code))
                .Select(pair => pair.Key)
                .ToList();
            var aggregates = entries.Where(entry => entry.Code == code).ToList();

            if (applicableLines.Count > 1)
                throw new InvalidOperationException("Several lines declare the same selective consumption tax and the signed document does not attribute it.");

            if (applicableLines.Count == 1 && aggregates.Count != 1)
                throw new InvalidOperationException("The selective consumption tax of a line cannot be attributed unambiguously.");

            if (applicableLines.Count == 1
                && aggregates[0].SelectiveConsumptionTax is null
                && aggregates[0].AdValoremSelectiveConsumptionTax is null)
                throw new InvalidOperationException("The signed selective consumption tax carries no specific or ad-valorem amount.");
        }

        var attributedEntries = entries.Select(entry =>
        {
            if (!IsSelectiveConsumptionTaxCode(entry.Code))
                return entry;

            var applicableLines = itemTaxCodes
                .Where(pair => pair.Value.Contains(entry.Code))
                .Select(pair => pair.Key)
                .ToList();

            if (applicableLines.Count == 0)
                return entry;

            return entry with { LineNumber = applicableLines[0] };
        }).ToList();

        return new EcfPrintAdditionalTaxes
        {
            SelectiveConsumptionTax = isc,
            TelecomDevelopmentTax = cdt,
            LegalTip = tip,
            Entries = attributedEntries
        };
    }

    private static IReadOnlyList<EcfItem> ItemsOf(XmlNode root)
    {
        var nodes = root.SelectNodes("DetallesItems/Item");
        var items = new List<EcfItem>();
        var line = 0;

        if (nodes is null)
            return items;

        foreach (XmlNode node in nodes)
        {
            line++;

            items.Add(new EcfItem
            {
                LineNumber = Integer(node.SelectSingleNode("NumeroLinea")) ?? line,
                TaxTreatment = TaxCodeOf(node.SelectSingleNode("IndicadorFacturacion")),
                Name = Text(node.SelectSingleNode("NombreItem")) ?? string.Empty,
                Kind = KindOf(node.SelectSingleNode("IndicadorBienoServicio")),
                Description = Text(node.SelectSingleNode("DescripcionItem")),
                Quantity = Money(node.SelectSingleNode("CantidadItem")) ?? 0m,
                UnitOfMeasure = Integer(node.SelectSingleNode("UnidadMedida")),
                UnitPrice = Money(node.SelectSingleNode("PrecioUnitarioItem")) ?? 0m,
                Amount = Money(node.SelectSingleNode("MontoItem")) ?? 0m,
                DiscountAmount = Money(node.SelectSingleNode("DescuentoMonto")),
                SurchargeAmount = Money(node.SelectSingleNode("RecargoMonto"))
            });
        }

        return items;
    }

    private static IReadOnlyList<EcfAdjustment> AdjustmentsOf(XmlNode root)
    {
        var nodes = root.SelectNodes("DescuentosORecargos/DescuentoORecargo");

        if (nodes is null)
            return [];

        var adjustments = new List<EcfAdjustment>();

        foreach (XmlNode node in nodes)
        {
            adjustments.Add(new EcfAdjustment
            {
                LineNumber = Integer(node.SelectSingleNode("NumeroLinea"))
                    ?? throw new InvalidOperationException("The signed document carries an adjustment without a line."),
                Kind = AdjustmentKindOf(node.SelectSingleNode("TipoAjuste")),
                FollowsNorm1007 = Flag(node.SelectSingleNode("IndicadorNorma1007")),
                Description = Text(node.SelectSingleNode("DescripcionDescuentooRecargo")),
                ValueKind = AdjustmentValueKindOf(node.SelectSingleNode("TipoValor")),
                Value = Money(node.SelectSingleNode("ValorDescuentooRecargo")),
                Amount = Money(node.SelectSingleNode("MontoDescuentooRecargo"))
            });
        }

        return adjustments;
    }

    private static EcfPrintPagination? PaginationOf(XmlNode root, IReadOnlyList<EcfItem> items)
    {
        var declaredTotal = Integer(root.SelectSingleNode("Encabezado/IdDoc/TotalPaginas"));
        var nodes = root.SelectNodes("Paginacion/Pagina");

        if (nodes is null || nodes.Count == 0)
        {
            if (declaredTotal is > 1)
                throw new InvalidOperationException("The signed document declares several pages without their pagination.");

            if (declaredTotal != 1)
                return null;

            if (items.Count == 0)
                throw new InvalidOperationException("The signed document declares one page without any line.");

            return new EcfPrintPagination
            {
                TotalPages = 1,
                Pages =
                [
                    new EcfPrintPage
                    {
                        Number = 1,
                        FirstLineNumber = items[0].LineNumber,
                        LastLineNumber = items[^1].LineNumber
                    }
                ]
            };
        }

        var pages = new List<EcfPrintPage>();
        var expectedLine = items.Count == 0 ? 1 : items[0].LineNumber;

        foreach (XmlNode node in nodes)
        {
            var number = Integer(node.SelectSingleNode("PaginaNo"))
                ?? throw new InvalidOperationException("The signed document carries a page without a number.");
            var firstLine = Integer(node.SelectSingleNode("NoLineaDesde"))
                ?? throw new InvalidOperationException("The signed document carries a page without its first line.");
            var lastLine = Integer(node.SelectSingleNode("NoLineaHasta"))
                ?? throw new InvalidOperationException("The signed document carries a page without its last line.");

            if (number != pages.Count + 1)
                throw new InvalidOperationException("The pages of the signed document are not in order.");
            if (firstLine != expectedLine || lastLine < firstLine)
                throw new InvalidOperationException("The lines of the signed document do not follow the order of its pages.");

            var selected = items
                .Where(item => item.LineNumber >= firstLine && item.LineNumber <= lastLine)
                .ToList();

            if (selected.Count != lastLine - firstLine + 1)
                throw new InvalidOperationException("A page of the signed document names lines that the document does not have.");

            pages.Add(new EcfPrintPage
            {
                Number = number,
                FirstLineNumber = firstLine,
                LastLineNumber = lastLine,
                TaxableSubtotal = Money(node.SelectSingleNode("SubtotalMontoGravadoPagina")),
                ExemptSubtotal = Money(node.SelectSingleNode("SubtotalExentoPagina")),
                ItbisSubtotal = Money(node.SelectSingleNode("SubtotalItbisPagina")),
                SelectiveConsumptionTaxSubtotal = Money(node.SelectSingleNode("SubtotalImpuestoAdicional/SubtotalImpuestoSelectivoConsumoEspecificoPagina")),
                OtherAdditionalTaxesSubtotal = Money(node.SelectSingleNode("SubtotalImpuestoAdicional/SubtotalOtrosImpuesto")),
                AdditionalTaxSubtotal = Money(node.SelectSingleNode("SubtotalImpuestoAdicionalPagina")),
                NonBillableSubtotal = Money(node.SelectSingleNode("SubtotalMontoNoFacturablePagina")),
                Total = Money(node.SelectSingleNode("MontoSubtotalPagina"))
            });

            expectedLine = lastLine + 1;
        }

        if (declaredTotal.HasValue && declaredTotal.Value != pages.Count)
            throw new InvalidOperationException("The signed document declares a number of pages that its pagination does not carry.");
        if (items.Count > 0 && pages[^1].LastLineNumber != items[^1].LineNumber)
            throw new InvalidOperationException("The pagination of the signed document does not cover all of its lines.");

        return new EcfPrintPagination
        {
            TotalPages = declaredTotal ?? pages.Count,
            Pages = pages
        };
    }

    private static EcfReference? ReferenceOf(XmlNode root)
    {
        var reference = root.SelectSingleNode("InformacionReferencia");

        if (reference is null)
            return null;

        return new EcfReference
        {
            ModifiedENCF = Text(reference.SelectSingleNode("NCFModificado")),
            OtherContributorTaxId = Text(reference.SelectSingleNode("RNCOtroContribuyente")),
            ModifiedAt = Day(reference.SelectSingleNode("FechaNCFModificado")),
            ModificationCode = ModificationCode(reference.SelectSingleNode("CodigoModificacion")),
            ModificationReason = Text(reference.SelectSingleNode("RazonModificacion"))
        };
    }

    private static void ValidateSequenceExpiry(EcfDocument document)
    {
        if (document.DocumentType is EcfDocumentType.Consumer or EcfDocumentType.CreditNote)
            return;

        if (document.Identity.SequenceExpiresAt is null)
            throw new InvalidOperationException("The signed document carries no expiry of the authorized sequence.");
    }

    private static void ValidateReference(EcfDocument document)
    {
        if (document.DocumentType is not (EcfDocumentType.DebitNote or EcfDocumentType.CreditNote))
            return;

        var reference = document.Reference;

        if (reference is null
            || string.IsNullOrWhiteSpace(reference.ModifiedENCF)
            || reference.ModifiedAt is null
            || reference.ModificationCode is null)
            throw new InvalidOperationException("The signed note carries no complete reference to the document it modifies.");
    }

    private static string AdditionalTaxCodeOf(XmlNode? node)
    {
        var value = Text(node)
            ?? throw new InvalidOperationException("The signed document carries an additional tax without a code.");

        if (value.Length != 3
            || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var code)
            || code < 1
            || code > 39)
            throw new InvalidOperationException($"The signed document carries an unknown additional tax code: {value}.");

        return value;
    }

    private static bool IsSelectiveConsumptionTaxCode(string code)
        => int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            && value is >= 6 and <= 39;

    private static EcfAdjustmentKind AdjustmentKindOf(XmlNode? node)
    {
        var value = Text(node)
            ?? throw new InvalidOperationException("The signed document carries an adjustment without a kind.");

        return value switch
        {
            "D" => EcfAdjustmentKind.Discount,
            "R" => EcfAdjustmentKind.Surcharge,
            _ => throw new InvalidOperationException($"The signed document carries an unknown adjustment kind: {value}.")
        };
    }

    private static EcfAdjustmentValueKind? AdjustmentValueKindOf(XmlNode? node)
    {
        var value = Text(node);

        if (value is null)
            return null;

        return value switch
        {
            "$" => EcfAdjustmentValueKind.Amount,
            "%" => EcfAdjustmentValueKind.Percentage,
            _ => throw new InvalidOperationException($"The signed document carries an unknown adjustment value kind: {value}.")
        };
    }

    private static TaxCode TaxCodeOf(XmlNode? node)
    {
        var value = Text(node)
            ?? throw new InvalidOperationException("The signed document carries a line without tax treatment.");

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var code)
            || !Enum.IsDefined((TaxCode)code))
            throw new InvalidOperationException($"The signed document carries an unknown tax treatment: {value}.");

        return (TaxCode)code;
    }

    private static EcfItemKind KindOf(XmlNode? node)
    {
        var value = Text(node);

        if (value is null || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var code))
            return EcfItemKind.Goods;

        return Enum.IsDefined((EcfItemKind)code) ? (EcfItemKind)code : EcfItemKind.Goods;
    }

    private static EcfPaymentType? PaymentType(XmlNode? node)
    {
        var value = Text(node);

        if (value is null || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var code))
            return null;

        return Enum.IsDefined((EcfPaymentType)code) ? (EcfPaymentType)code : null;
    }

    private static EcfModificationCode? ModificationCode(XmlNode? node)
    {
        var value = Text(node);

        if (value is null || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var code))
            return null;

        if (!Enum.IsDefined((EcfModificationCode)code))
            throw new InvalidOperationException($"The signed document carries an unknown modification code: {value}.");

        return (EcfModificationCode)code;
    }

    private static DateTime SignedAtOf(XmlNode root)
        => Moment(root.SelectSingleNode("FechaHoraFirma"))
            ?? throw new InvalidOperationException("The signed document carries no date of signature.");

    private static void VerifySignature(XmlDocument document)
    {
        var signatures = document.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl);

        if (signatures.Count != 1)
            throw new InvalidOperationException("The signed document does not carry exactly one digital signature.");

        try
        {
            var signature = (XmlElement)signatures[0]!;
            var verifier = new SignedXml(document);
            verifier.LoadXml(signature);

            if (verifier.SignedInfo?.References.Count != 1
                || verifier.SignedInfo.References[0] is not Reference reference
                || reference.Uri != string.Empty)
                throw new InvalidOperationException("The digital signature does not cover the whole signed document.");

            var namespaces = new XmlNamespaceManager(document.NameTable);
            namespaces.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);
            var certificateNode = signature.SelectSingleNode(".//ds:X509Data/ds:X509Certificate", namespaces)
                ?? throw new InvalidOperationException("The digital signature carries no certificate.");
            var encoded = certificateNode.InnerText
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Replace(" ", string.Empty);
            var certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(encoded));

            if (!verifier.CheckSignature(certificate, true))
                throw new InvalidOperationException("The digital signature of the signed document does not verify.");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("The digital signature of the signed document cannot be verified.", exception);
        }
    }

    private static string? Text(XmlNode? node)
    {
        var value = node?.InnerText?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static decimal? Money(XmlNode? node)
    {
        var value = Text(node);

        if (value is null)
            return null;

        if (!decimal.TryParse(
                value,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var amount))
            throw new InvalidOperationException($"The signed document carries an amount that is not a number: {value}.");

        return amount;
    }

    private static int? Integer(XmlNode? node)
    {
        var value = Text(node);

        if (value is null)
            return null;

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            throw new InvalidOperationException($"The signed document carries a number that is not an integer: {value}.");

        return number;
    }

    private static DateTime? Day(XmlNode? node)
    {
        var value = Text(node);

        if (value is null)
            return null;

        if (!DateTime.TryParseExact(
                value,
                "dd-MM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var day))
            throw new InvalidOperationException($"The signed document carries a date that is not a day: {value}.");

        return day;
    }

    private static DateTime? Moment(XmlNode? node)
    {
        var value = Text(node);

        if (value is null)
            return null;

        if (!DateTime.TryParseExact(
                value,
                "dd-MM-yyyy HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var moment))
            throw new InvalidOperationException($"The signed document carries a date that is not a moment: {value}.");

        return moment;
    }

    private static bool? Flag(XmlNode? node)
    {
        var value = Text(node);

        if (value is null)
            return null;

        if (value is not ("0" or "1"))
            throw new InvalidOperationException($"The signed document carries a flag that is not zero or one: {value}.");

        return value == "1";
    }
}
