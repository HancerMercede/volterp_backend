using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Volterp.ElectronicInvoicing.Printing;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.ElectronicInvoicing.Xml;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Printing;

// The printed representation: the paper the receiver keeps when it is not
// electronic. Its layout is its own and it comes out of the buyer: a buyer
// with a tax id gets the full layout and a buyer without one the simplified
// one of the final consumer.
public class EcfPrintRendererTests
{
    private const string SecurityCode = "BoXjC8";
    private const string TimbreUrl =
        "https://ecf.dgii.gov.do/testecf/consultatimbre?rncemisor=130000001&encf=E310000000001&codigoseguridad=BoXjC8";
    private const string QrSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 610 610\">"
        + "<path fill=\"#fff\" d=\"M0 0h610v610H0z\"/>"
        + "<path fill=\"#000\" d=\"M40 40h530v530H40z\"/></svg>";

    [Fact]
    public void ABuyerWithATaxIdGetsTheFullLayout()
    {
        var html = Render(Document());

        html.Should().Contain("data-template=\"registered-buyer\"");
        html.Should().Contain("130000002", "the tax id of the buyer is on the representation");
        html.Should().Contain("Cliente Corporativo SRL");
    }

    [Fact]
    public void ABuyerWithoutATaxIdGetsTheSimplifiedLayout()
    {
        var html = Render(Document(buyerTaxId: null));

        html.Should().Contain("data-template=\"final-consumer\"");
        html.Should().NotContain("Cliente Corporativo SRL", "the simplified layout has no buyer block");
        html.Should().NotContain("130000002");
    }

    [Fact]
    public void TheHeaderCarriesTheIssuerOnTheLeftAndTheDocumentOnTheRight()
    {
        var html = Render(Document());

        html.Should().Contain("Volterp Componentes SRL");
        html.Should().Contain("Nombre Comercial Volterp");
        html.Should().Contain("Sucursal: Sucursal Norte");
        html.Should().Contain("RNC: 130000001");
        html.Should().Contain("Calle Duarte 45, Santo Domingo");
        html.Should().Contain("010100", "the municipality of the issuer");
        html.Should().Contain("010000", "the province of the issuer");        html.Should().Contain("Fecha de emisión: 14-11-2026");

        html.Should().Contain("Factura de Crédito Fiscal Electrónica");
        html.Should().Contain("e-NCF: E310000000001");
        html.Should().Contain("Fecha Vencimiento: 31-12-2028",
            "the expiry of the authorized sequence is the date the e-NCF expires");
        html.Should().Contain("<td>Condición de pago</td><td>30 días</td>");
    }

    [Fact]
    public void TheRepresentationCarriesTheLinesTheTotalsAndTheIssuer()
    {
        var html = Render(Document());

        html.Should().Contain("Café molido");
        html.Should().Contain("2.5", "the quantity of the line");
        html.Should().Contain("80.00", "the price of the line");
        html.Should().Contain("36.00", "the ITBIS of the line");
        html.Should().Contain("200.00", "the value of the line");

        html.Should().Contain("Subtotal Gravado");
        html.Should().Contain("Total ITBIS");
        html.Should().Contain("241.00", "the total of the document");
    }

    [Fact]
    public void TheLinesCarryTheDiscountTheSurchargeAndTheExemptIndicator()
    {
        var html = Render(Document());

        html.Should().Contain("Descuento");
        html.Should().Contain("Recargo");
        html.Should().Contain("10.00", "the discount of the line");
        html.Should().Contain("5.00", "the surcharge of the line");
        html.Should().Contain(">E<", "the exempt line carries the E to the left of its description");
    }

    [Fact]
    public void TheLineDescriptionIsTheSignedItemName()
    {
        var document = Document();
        var item = document.Items[0] with
        {
            Name = "Nombre oficial del ítem",
            Description = "Descripción complementaria"
        };
        document = document with { Items = [item] };

        var html = Render(document);

        html.Should().Contain("Nombre oficial del ítem");
        html.Should().NotContain("Descripción complementaria",
            "the RI description is NombreItem, not the optional DescripcionItem");
    }

    [Fact]
    public void AnExclusiveLineAmountUsesTheItbisRateAsTheTaxBase()
    {
        var signed = SignedWithTaxAmounts(includeItbis: false);

        signed.Document.Totals.ItbisTotal.Should().Be(21.24m);

        var html = Render(signed);

        html.Should().Contain("<td class=\"amount\">21.24</td>");
    }

    [Fact]
    public void AnInclusiveLineAmountExtractsTheItbisAlreadyContainedInTheAmount()
    {
        var signed = SignedWithTaxAmounts(includeItbis: true);

        signed.Document.Totals.ItbisTotal.Should().Be(18m);

        var html = Render(signed);

        html.Should().Contain("<td class=\"amount\">18.00</td>");
        html.Should().NotContain("21.24");
    }

    [Fact]
    public void TheLineCarriesTheSelectiveConsumptionTaxesWhenTheSignedAttributionIsUnambiguous()
    {
        var signed = SignedWithIscCode006(itemCount: 1, declareTaxOnItems: true);

        signed.AdditionalTaxes.SelectiveConsumptionTax.Should().Be(35m,
            "the signed aggregate remains in the totals");

        var html = Render(signed);

        html.Should().Contain(">ISC Específico</th>");
        html.Should().Contain(">ISC Ad-Valorem</th>");
        html.Should().Contain("<td class=\"amount\">30.00</td>");
        html.Should().Contain("<td class=\"amount\">5.00</td>");
    }

    [Fact]
    public void AnAggregateSelectiveTaxWithoutAnApplicableLineLeavesTheLineColumnsEmpty()
    {
        var signed = SignedWithIscCode006(itemCount: 1, declareTaxOnItems: false);

        signed.AdditionalTaxes.SelectiveConsumptionTax.Should().Be(35m,
            "the signed aggregate remains in the totals");

        var html = Render(signed);
        var row = Regex.Match(
            html,
            "<tr><td class=\"number\">1</td>.*?</tr>",
            RegexOptions.Singleline).Value;

        html.Should().Contain(">ISC Específico</th>");
        html.Should().Contain(">ISC Ad-Valorem</th>");
        row.Should().NotContain("30.00");
        row.Should().NotContain("5.00");
    }

    [Fact]
    public void SeveralLinesWithTheSameSelectiveTaxCannotBeAttributedAndFailClosed()
    {
        var read = () => SignedWithIscCode006(itemCount: 2, declareTaxOnItems: true);

        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TheTotalsCarryTheGlobalDiscountsAndSurcharges()
    {
        var document = Document() with
        {
            Adjustments =
            [
                new EcfAdjustment
                {
                    LineNumber = 1,
                    Kind = EcfAdjustmentKind.Discount,
                    Description = "Promoción de apertura",
                    ValueKind = EcfAdjustmentValueKind.Percentage,
                    Value = 10m,
                    Amount = 20m
                }
            ]
        };

        var html = Render(document);

        html.Should().Contain("Descripción de Descuento o Recargo");
        html.Should().Contain("Promoción de apertura");
        html.Should().Contain("Descuento o Recargo en %");
        html.Should().Contain("10.00%");
        html.Should().Contain("Monto de Descuento o Recargo");
        html.Should().Contain("20.00");
    }

    [Fact]
    public void TheTotalsCarryTheAdditionalTaxesTheRetentionsAndTheNonBillableAmount()
    {
        var html = Render(
            Document(),
            new EcfPrintAdditionalTaxes
            {
                SelectiveConsumptionTax = 30m,
                TelecomDevelopmentTax = 12m,
                LegalTip = 50m
            });

        html.Should().Contain("Total ISC");
        html.Should().Contain("30.00");
        html.Should().Contain("CDT");
        html.Should().Contain("12.00");
        html.Should().Contain("Propina legal");
        html.Should().Contain("50.00");
        html.Should().Contain("ITBIS retenido");
        html.Should().Contain("ISR retención");
        html.Should().Contain("Monto no facturable");
    }

    [Fact]
    public void AnAdditionalTaxThatIsNotSelectiveIsNotShownAsIsc()
    {
        var document = Document();
        var totals = document.Totals;
        document = document with
        {
            Totals = totals with { AdditionalTaxTotal = 50m, Total = totals.Total + 50m }
        };

        var html = Render(
            document,
            new EcfPrintAdditionalTaxes { LegalTip = 50m });

        html.Should().NotContain("<td>Total ISC</td>");
        html.Should().Contain("<td>Propina legal</td><td class=\"amount\">50.00</td>");
    }

    [Fact]
    public void TheNotesCarryTheModifiedDocumentAndTheModificationCode()
    {
        var html = Render(Document(EcfDocumentType.DebitNote, reference: new EcfReference
        {
            ModifiedENCF = "E310000000009",
            ModifiedAt = new DateTime(2026, 11, 1),
            ModificationCode = EcfModificationCode.CorrectsAmounts
        }));

        html.Should().Contain("e-NCF Modificado: E310000000009");
        html.Should().Contain("Fecha NCF Modificado: 01-11-2026");
        html.Should().Contain("Código de Modificación: Corrige montos del NCF modificado");
    }

    [Fact]
    public void TheCreditNoteCarriesTheDocumentItModifies()
    {
        var html = Render(Document(EcfDocumentType.CreditNote));

        html.Should().Contain("Nota de Crédito Electrónica");
        html.Should().Contain("e-NCF Modificado: E310000000009");
        html.Should().Contain("Código de Modificación: Corrige montos del NCF modificado");
    }

    [Fact]
    public void ARepresentationOfADocumentThatIsNotANoteCarriesNoModification()
    {
        var html = Render(Document());

        html.Should().NotContain("e-NCF Modificado");
        html.Should().NotContain("Código de Modificación");
    }

    [Fact]
    public void TheRepresentationCarriesTheSecurityCodeAndTheDateOfTheSignature()
    {
        var html = Render(Document());

        html.Should().Contain("Código de Seguridad");
        html.Should().Contain(SecurityCode);
        html.Should().Contain("Fecha de Firma Digital");
        html.Should().Contain("14-11-2026 10:30:00");
    }

    [Fact]
    public void TheQrGoesAtTheLowerLeftWithTheSecurityCodeBelowIt()
    {
        var html = Render(Document());

        // The QR is at least 22 x 22 mm with a margin of 3 mm on its sides,
        // and it starts at least two centimeters away from the edge of the
        // sheet.
        html.Should().Contain("@page { size: letter; margin: 20mm; }");
        html.Should().Contain(".qr { width: 32mm; height: 32mm; padding: 3mm; }");
        html.Should().Contain("footer { position: absolute; bottom: 0; left: 0; right: 0;");

        var qr = html.IndexOf("<div class=\"qr\">", StringComparison.Ordinal);
        var security = html.IndexOf("Código de Seguridad", StringComparison.Ordinal);
        var signature = html.IndexOf("Fecha de Firma Digital", StringComparison.Ordinal);

        qr.Should().BeGreaterThan(0);
        security.Should().BeGreaterThan(qr, "the security code goes below the QR");
        signature.Should().BeGreaterThan(qr, "the date of the signature goes below the QR");
    }

    [Fact]
    public void TheRepresentationCarriesTheQrOfTheTimbre()
    {
        var html = Render(Document());

        // The URL travels escaped, the way the HTML carries an ampersand.
        html.Should().Contain(TimbreUrl.Replace("&", "&amp;"), "the QR opens the timbre of the document");
        html.Should().Contain("<svg", "the QR travels inside the representation");
    }

    [Fact]
    public void ThePrintedQrSymbolIsAtLeastTwentyTwoMillimeters()
    {
        var html = Render(Document());
        var css = Regex.Match(
            html,
            @"\.qr \{ width: (?<width>[\d.]+)mm; height: (?<height>[\d.]+)mm; padding: (?<padding>[\d.]+)mm; \}");
        var width = decimal.Parse(css.Groups["width"].Value, CultureInfo.InvariantCulture);
        var padding = decimal.Parse(css.Groups["padding"].Value, CultureInfo.InvariantCulture);

        var start = html.IndexOf("<svg", StringComparison.Ordinal);
        var end = html.IndexOf("</svg>", start, StringComparison.Ordinal) + "</svg>".Length;
        var svg = XDocument.Parse(html[start..end]);
        var viewBox = svg.Root!.Attribute("viewBox")!.Value.Split(' ');
        var viewBoxWidth = decimal.Parse(viewBox[2], CultureInfo.InvariantCulture);
        var symbolPath = svg.Descendants().Single(path => path.Attribute("fill")?.Value == "#000").Attribute("d")!.Value;
        var symbol = Regex.Match(symbolPath, @"M(?<x>\d+) (?<y>\d+)h(?<width>\d+)v");
        var symbolWidth = decimal.Parse(symbol.Groups["width"].Value, CultureInfo.InvariantCulture);
        var renderedSvgWidth = width - (2 * padding);
        var printedSymbolWidth = renderedSvgWidth * symbolWidth / viewBoxWidth;

        printedSymbolWidth.Should().BeGreaterThanOrEqualTo(22m);
        html.Should().Contain("@page { size: letter; margin: 20mm; }");
        html.Should().Contain("footer { position: absolute; bottom: 0; left: 0; right: 0;");
    }

    [Fact]
    public void ARepresentationWithManyLinesTakesMoreThanOnePage()
    {
        var pagination = new EcfPrintPagination
        {
            TotalPages = 2,
            Pages =
            [
                new EcfPrintPage
                {
                    Number = 1,
                    FirstLineNumber = 1,
                    LastLineNumber = 20,
                    TaxableSubtotal = 1600m,
                    ExemptSubtotal = 5m,
                    ItbisSubtotal = 288m,
                    SelectiveConsumptionTaxSubtotal = 10m,
                    OtherAdditionalTaxesSubtotal = 5m,
                    AdditionalTaxSubtotal = 15m,
                    Total = 1908m
                },
                new EcfPrintPage
                {
                    Number = 2,
                    FirstLineNumber = 21,
                    LastLineNumber = 25
                }
            ]
        };

        var html = Render(Document(lines: 25), pagination: pagination);

        html.Should().Contain("Página 1 de 2");
        html.Should().Contain("Subtotal Gravado Página");
        html.Should().Contain("Subtotal Exento Página");
        html.Should().Contain("Subtotal ITBIS Página");
        html.Should().Contain("Subtotal Impuesto Selectivo al Consumo Página");
        html.Should().Contain("Subtotal Otros Impuestos Adicionales Página");
        html.Should().Contain("Subtotal Impuesto Adicional Página");
        html.Should().Contain("Monto total Página");
        html.Should().Contain("1,908.00");
        html.Should().NotContain("Página 2 de 2",
            "the last page carries the totals of the document, not the pagination");
    }

    [Fact]
    public void ARepresentationOfOnePageHasNoPagination()
    {
        var html = Render(Document());

        html.Should().NotContain("Página ");
    }

    [Fact]
    public void PaginationIsDeterministic()
    {
        Render(Document(lines: 25)).Should().Be(Render(Document(lines: 25)));
    }

    [Fact]
    public void TheTypeOfTheDocumentIsTheOfficialName()
    {
        // The debit note is the 33 and the credit note is the 34: a layout
        // that swaps them prints a document of the wrong type, and the
        // mistake is paid at the counter.
        Render(Document(EcfDocumentType.FiscalCredit)).Should().Contain("Factura de Crédito Fiscal Electrónica");
        Render(Document(EcfDocumentType.Consumer)).Should().Contain("Factura de Consumo Electrónica");
        Render(Document(EcfDocumentType.DebitNote)).Should().Contain("Nota de Débito Electrónica");
        Render(Document(EcfDocumentType.CreditNote)).Should().Contain("Nota de Crédito Electrónica");
    }

    [Fact]
    public void TheValuesOfTheDocumentAreEscaped()
    {
        var document = Document() with
        {
            Buyer = new EcfBuyer { TaxId = "130000002", BusinessName = "<script>alert(1)</script>" }
        };

        var html = Render(document);

        html.Should().NotContain("<script>");
        html.Should().Contain("&lt;script&gt;");
    }

    private static string Render(
        EcfDocument document,
        EcfPrintAdditionalTaxes? additionalTaxes = null,
        EcfPrintPagination? pagination = null,
        bool itemAmountsIncludeItbis = false)
        => EcfPrintRenderer.Render(new EcfPrintRequest
        {
            Document = document,
            AdditionalTaxes = additionalTaxes,
            Pagination = pagination,
            ItemAmountsIncludeItbis = itemAmountsIncludeItbis,
            SecurityCode = SecurityCode,
            QrUrl = TimbreUrl,
            QrImageSvg = QrSvg
        });

    private static string Render(EcfSignedDocument signed)
        => Render(
            signed.Document,
            signed.AdditionalTaxes,
            signed.Pagination,
            signed.ItemAmountsIncludeItbis);

    private static EcfSignedDocument SignedWithTaxAmounts(bool includeItbis)
    {
        var document = Document();
        var item = document.Items[0] with
        {
            Quantity = 1m,
            UnitPrice = includeItbis ? 100m : 118m,
            Amount = includeItbis ? 100m : 118m
        };
        var items = new[] { item };
        document = document with
        {
            Items = items,
            Totals = EcfTotalsCalculator.Calculate(items)
        };

        var xml = new EcfXmlWriter().Write(document);

        if (includeItbis)
            xml = xml.Replace("<MontoItem>100.00</MontoItem>", "<MontoItem>118.00</MontoItem>");

        xml = xml.Replace(
            "</FechaVencimientoSecuencia>",
            $"</FechaVencimientoSecuencia><IndicadorMontoGravado>{(includeItbis ? 1 : 0)}</IndicadorMontoGravado>");

        return EcfSignedDocumentReader.Read(SignedXml(xml));
    }

    private static EcfSignedDocument SignedWithIscCode006(int itemCount, bool declareTaxOnItems)
    {
        var document = Document();
        var items = Enumerable.Range(1, itemCount)
            .Select(line => new EcfItem
            {
                LineNumber = line,
                TaxTreatment = TaxCode.ItbisTasa1,
                Name = $"Café molido {line}",
                Kind = EcfItemKind.Goods,
                Quantity = 1m,
                UnitPrice = 100m,
                Amount = 100m
            })
            .ToArray();
        var totals = EcfTotalsCalculator.Calculate(items);
        document = document with
        {
            Items = items,
            Totals = totals with { AdditionalTaxTotal = 35m, Total = totals.Total + 35m }
        };

        var xml = new EcfXmlWriter().Write(document);

        if (declareTaxOnItems)
        {
            xml = xml.Replace(
                "<MontoItem>",
                "<TablaImpuestoAdicional><ImpuestoAdicional><TipoImpuesto>006</TipoImpuesto>"
                + "</ImpuestoAdicional></TablaImpuestoAdicional><MontoItem>");
        }

        xml = xml.Replace(
            "<MontoTotal>",
            "<ImpuestosAdicionales><ImpuestoAdicional><TipoImpuesto>006</TipoImpuesto>"
            + "<TasaImpuestoAdicional>10.00</TasaImpuestoAdicional>"
            + "<MontoImpuestoSelectivoConsumoEspecifico>30.00</MontoImpuestoSelectivoConsumoEspecifico>"
            + "<MontoImpuestoSelectivoConsumoAdvalorem>5.00</MontoImpuestoSelectivoConsumoAdvalorem>"
            + "</ImpuestoAdicional></ImpuestosAdicionales><MontoTotal>");

        return EcfSignedDocumentReader.Read(SignedXml(xml));
    }

    private static string SignedXml(string xml)
        => new XmlDsigEcfSigner().Sign(xml, TestCertificate.Create());

    private static EcfDocument Document(
        EcfDocumentType documentType = EcfDocumentType.FiscalCredit,
        string? buyerTaxId = "130000002",
        EcfReference? reference = null,
        int lines = 1)
    {
        var items = new List<EcfItem>
        {
            new()
            {
                LineNumber = 1,
                TaxTreatment = TaxCode.ItbisTasa1,
                Name = "Café molido",
                Kind = EcfItemKind.Goods,
                Quantity = 2.5m,
                UnitPrice = 80m,
                Amount = 200m,
                DiscountAmount = 10m
            },
            new()
            {
                LineNumber = 2,
                TaxTreatment = TaxCode.Exento,
                Name = "Bolsa reutilizable",
                Kind = EcfItemKind.Goods,
                Quantity = 1m,
                UnitPrice = 5m,
                Amount = 5m,
                SurchargeAmount = 5m
            }
        };

        for (var line = items.Count; line < lines; line++)
        {
            items.Add(new EcfItem
            {
                LineNumber = line + 1,
                TaxTreatment = TaxCode.ItbisTasa1,
                Name = "Café molido",
                Kind = EcfItemKind.Goods,
                Quantity = 1m,
                UnitPrice = 80m,
                Amount = 80m
            });
        }

        reference ??= documentType is EcfDocumentType.DebitNote or EcfDocumentType.CreditNote
            ? new EcfReference
            {
                ModifiedENCF = "E310000000009",
                ModifiedAt = new DateTime(2026, 11, 1),
                ModificationCode = EcfModificationCode.CorrectsAmounts
            }
            : null;

        return new EcfDocument
        {
            DocumentType = documentType,
            Identity = new EcfIdentity
            {
                LineAmountsIncludeItbis = false,
                ENCF = $"E{(int)documentType:D2}0000000001",
                SequenceExpiresAt = new DateTime(2028, 12, 31),
                TypeIncome = EcfTypeIncome.Operations,
                PaymentType = EcfPaymentType.Credit,
                PaymentDueDate = new DateTime(2026, 12, 14),
                PaymentTerm = "30 días"
            },
            Issuer = new EcfIssuer
            {
                TaxId = "130000001",
                BusinessName = "Volterp Componentes SRL",
                TradeName = "Nombre Comercial Volterp",
                Branch = "Sucursal Norte",
                Address = "Calle Duarte 45, Santo Domingo",
                Municipality = "010100",
                Province = "010000",
                PhoneNumbers = ["809-555-0100"],
                Email = "facturacion@volterp.test",
                IssuedAt = new DateTime(2026, 11, 14)
            },
            Buyer = buyerTaxId is null
                ? new EcfBuyer { BusinessName = "Consumidor Final" }
                : new EcfBuyer { TaxId = buyerTaxId, BusinessName = "Cliente Corporativo SRL", Address = "Avenida Independencia 45" },
            Totals = EcfTotalsCalculator.Calculate(items) with
            {
                ItbisWithheld = 5m,
                IncomeTaxWithheld = 3m,
                NonBillableTotal = 7m
            },
            Items = items,
            Reference = reference,
            SignedAt = new DateTime(2026, 11, 14, 10, 30, 0)
        };
    }
}
