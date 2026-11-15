using System.Xml;
using FluentAssertions;
using Volterp.ElectronicInvoicing.Printing;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.ElectronicInvoicing.Xml;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Printing;

// The reader that takes a signed e-CF back into the model of the engine: the
// printed representation is built from what was signed, not from what the
// ERP happens to hold today.
public class EcfSignedDocumentReaderTests
{
    [Fact]
    public void ItReadsWhatWasSigned()
    {
        var read = EcfSignedDocumentReader.Read(Signed(Document())).Document;

        read.DocumentType.Should().Be(EcfDocumentType.FiscalCredit);
        read.Identity.ENCF.Should().Be("E310000000001");
        read.Identity.PaymentType.Should().Be(EcfPaymentType.Cash);
        read.Identity.SequenceExpiresAt.Should().Be(new DateTime(2028, 12, 31));

        read.Issuer.TaxId.Should().Be("130000001");
        read.Issuer.BusinessName.Should().Be("Volterp Componentes SRL");
        read.Issuer.TradeName.Should().Be("Nombre Comercial Volterp");
        read.Issuer.Branch.Should().Be("Sucursal Norte");
        read.Issuer.Address.Should().Be("Calle Duarte 45, Santo Domingo");
        read.Issuer.Municipality.Should().Be("010100");
        read.Issuer.Province.Should().Be("010000");
        read.Issuer.IssuedAt.Should().Be(new DateTime(2026, 11, 14));

        read.Buyer.Should().NotBeNull();
        read.Buyer!.TaxId.Should().Be("130000002");
        read.Buyer.BusinessName.Should().Be("Cliente Corporativo SRL");

        read.Totals.TaxableTotal.Should().Be(200m);
        read.Totals.ItbisTotal.Should().Be(36m);
        read.Totals.Total.Should().Be(236m);

        read.Items.Should().HaveCount(1);
        read.Items[0].LineNumber.Should().Be(1);
        read.Items[0].TaxTreatment.Should().Be(TaxCode.ItbisTasa1);
        read.Items[0].Name.Should().Be("Café molido");
        read.Items[0].Quantity.Should().Be(2.5m);
        read.Items[0].UnitPrice.Should().Be(80m);
        read.Items[0].Amount.Should().Be(200m);

        read.SignedAt.Should().Be(new DateTime(2026, 11, 14, 10, 30, 0));
    }

    [Fact]
    public void ItReadsTheDiscountAndTheSurchargeOfTheLines()
    {
        var read = EcfSignedDocumentReader.Read(Signed(Document())).Document;

        read.Items[0].DiscountAmount.Should().Be(10m);
        read.Items[0].SurchargeAmount.Should().BeNull();
    }

    [Fact]
    public void ItReadsTheDocumentTheNoteModifies()
    {
        var read = EcfSignedDocumentReader.Read(Signed(Note())).Document;

        read.Reference.Should().NotBeNull();
        read.Reference!.ModifiedENCF.Should().Be("E310000000009");
        read.Reference.ModificationCode.Should().Be(EcfModificationCode.CorrectsAmounts);
    }

    [Fact]
    public void ItReadsTheAdditionalTaxesOfTheTable()
    {
        var document = Document();
        var totals = document.Totals;

        var xml = new EcfXmlWriter().Write(document with
        {
            Totals = totals with { AdditionalTaxTotal = 92m, Total = totals.Total + 92m }
        });

        // The table of additional taxes says which tax every amount is.
        xml = xml.Replace(
            "<MontoImpuestoAdicional>92.00</MontoImpuestoAdicional>",
            "<MontoImpuestoAdicional>92.00</MontoImpuestoAdicional>"
            + "<ImpuestosAdicionales>"
            + "<ImpuestoAdicional><TipoImpuesto>006</TipoImpuesto>"
            + "<TasaImpuestoAdicional>10.00</TasaImpuestoAdicional>"
            + "<MontoImpuestoSelectivoConsumoEspecifico>30.00</MontoImpuestoSelectivoConsumoEspecifico>"
            + "</ImpuestoAdicional>"
            + "<ImpuestoAdicional><TipoImpuesto>001</TipoImpuesto>"
            + "<TasaImpuestoAdicional>10.00</TasaImpuestoAdicional>"
            + "<OtrosImpuestosAdicionales>50.00</OtrosImpuestosAdicionales>"
            + "</ImpuestoAdicional>"
            + "<ImpuestoAdicional><TipoImpuesto>002</TipoImpuesto>"
            + "<TasaImpuestoAdicional>10.00</TasaImpuestoAdicional>"
            + "<OtrosImpuestosAdicionales>12.00</OtrosImpuestosAdicionales>"
            + "</ImpuestoAdicional>"
            + "</ImpuestosAdicionales>");

        var read = EcfSignedDocumentReader.Read(SignedXml(xml));

        read.AdditionalTaxes.SelectiveConsumptionTax.Should().Be(30m);
        read.AdditionalTaxes.LegalTip.Should().Be(50m);
        read.AdditionalTaxes.TelecomDevelopmentTax.Should().Be(12m);
    }

    [Fact]
    public void WithoutATableTheWholeAdditionalAmountIsTheSelectiveConsumptionTax()
    {
        var document = Document();
        var totals = document.Totals;

        var read = EcfSignedDocumentReader.Read(Signed(document with
        {
            Totals = totals with { AdditionalTaxTotal = 30m, Total = totals.Total + 30m }
        }));

        read.AdditionalTaxes.SelectiveConsumptionTax.Should().Be(30m);
        read.AdditionalTaxes.LegalTip.Should().BeNull();
        read.AdditionalTaxes.TelecomDevelopmentTax.Should().BeNull();
    }

    [Fact]
    public void ABuyerWithoutATaxIdIsReadAsNoBuyerAtAll()
    {
        // The consumption invoice names the buyer, but the tax id of the
        // final consumer is not there.
        var read = EcfSignedDocumentReader.Read(Signed(Document(buyerTaxId: null))).Document;

        read.Buyer.Should().NotBeNull();
        read.Buyer!.TaxId.Should().BeNull();
        read.Buyer.BusinessName.Should().Be("Consumidor Final");
    }

    [Fact]
    public void ItReadsTheGlobalDiscountsAndSurcharges()
    {
        var xml = new EcfXmlWriter().Write(Document())
            .Replace(
                "</DetallesItems>",
                "</DetallesItems><DescuentosORecargos><DescuentoORecargo>"
                + "<NumeroLinea>1</NumeroLinea><TipoAjuste>D</TipoAjuste>"
                + "<IndicadorNorma1007>1</IndicadorNorma1007>"
                + "<DescripcionDescuentooRecargo>Promoción de apertura</DescripcionDescuentooRecargo>"
                + "<TipoValor>%</TipoValor><ValorDescuentooRecargo>10.00</ValorDescuentooRecargo>"
                + "<MontoDescuentooRecargo>20.00</MontoDescuentooRecargo>"
                + "</DescuentoORecargo></DescuentosORecargos>");

        var read = EcfSignedDocumentReader.Read(SignedXml(xml));

        read.Document.Adjustments.Should().ContainSingle();
        read.Document.Adjustments[0].LineNumber.Should().Be(1);
        read.Document.Adjustments[0].Kind.Should().Be(EcfAdjustmentKind.Discount);
        read.Document.Adjustments[0].Description.Should().Be("Promoción de apertura");
        read.Document.Adjustments[0].ValueKind.Should().Be(EcfAdjustmentValueKind.Percentage);
        read.Document.Adjustments[0].Value.Should().Be(10m);
        read.Document.Adjustments[0].Amount.Should().Be(20m);
    }

    [Fact]
    public void ItReadsThePaginationDeclaredByTheSignedDocument()
    {
        var document = Document();
        var items = document.Items
            .Append(new EcfItem
            {
                LineNumber = 2,
                TaxTreatment = TaxCode.ItbisTasa1,
                Name = "Café en grano",
                Kind = EcfItemKind.Goods,
                Quantity = 1m,
                UnitPrice = 80m,
                Amount = 80m
            })
            .ToList();
        var xml = new EcfXmlWriter().Write(document with
        {
            Items = items,
            Totals = EcfTotalsCalculator.Calculate(items)
        })
            .Replace("</IdDoc>", "<TotalPaginas>2</TotalPaginas></IdDoc>")
            .Replace(
                "<FechaHoraFirma>",
                "<Paginacion><Pagina><PaginaNo>1</PaginaNo><NoLineaDesde>1</NoLineaDesde>"
                + "<NoLineaHasta>1</NoLineaHasta><SubtotalMontoGravadoPagina>200.00</SubtotalMontoGravadoPagina>"
                + "<SubtotalItbisPagina>36.00</SubtotalItbisPagina><MontoSubtotalPagina>236.00</MontoSubtotalPagina>"
                + "</Pagina><Pagina><PaginaNo>2</PaginaNo><NoLineaDesde>2</NoLineaDesde>"
                + "<NoLineaHasta>2</NoLineaHasta><SubtotalMontoGravadoPagina>80.00</SubtotalMontoGravadoPagina>"
                + "<SubtotalItbisPagina>14.40</SubtotalItbisPagina><MontoSubtotalPagina>94.40</MontoSubtotalPagina>"
                + "</Pagina></Paginacion><FechaHoraFirma>");

        var pagination = EcfSignedDocumentReader.Read(SignedXml(xml)).Pagination;

        pagination.Should().NotBeNull();
        pagination!.TotalPages.Should().Be(2);
        pagination.Pages.Should().HaveCount(2);
        pagination.Pages[0].FirstLineNumber.Should().Be(1);
        pagination.Pages[0].LastLineNumber.Should().Be(1);
        pagination.Pages[0].TaxableSubtotal.Should().Be(200m);
        pagination.Pages[0].ItbisSubtotal.Should().Be(36m);
        pagination.Pages[0].Total.Should().Be(236m);
        pagination.Pages[1].FirstLineNumber.Should().Be(2);
        pagination.Pages[1].LastLineNumber.Should().Be(2);
    }

    [Fact]
    public void ItFailsClosedWhenTheSignedDocumentHasNoSequenceExpiry()
    {
        var document = new XmlDocument();
        document.LoadXml(new EcfXmlWriter().Write(Document()));
        var expiry = document.SelectSingleNode("/ECF/Encabezado/IdDoc/FechaVencimientoSecuencia")!;
        expiry.ParentNode!.RemoveChild(expiry);

        var read = () => EcfSignedDocumentReader.Read(SignedXml(document.OuterXml));

        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItFailsClosedWhenTheIssuerHasNoAddress()
    {
        var document = new XmlDocument();
        document.LoadXml(new EcfXmlWriter().Write(Document()));
        document.SelectSingleNode("/ECF/Encabezado/Emisor/DireccionEmisor")!
            .ParentNode!.RemoveChild(document.SelectSingleNode("/ECF/Encabezado/Emisor/DireccionEmisor")!);

        var read = () => EcfSignedDocumentReader.Read(SignedXml(document.OuterXml));

        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItFailsClosedWhenANoteHasNoReference()
    {
        var document = new XmlDocument();
        document.LoadXml(new EcfXmlWriter().Write(Note()));
        var reference = document.SelectSingleNode("/ECF/InformacionReferencia")!;
        reference.ParentNode!.RemoveChild(reference);

        var read = () => EcfSignedDocumentReader.Read(SignedXml(document.OuterXml));

        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItFailsClosedWhenTheSignatureDoesNotVerify()
    {
        var tampered = Signed(Document()).Replace("Café molido", "Café molido alterado");

        var read = () => EcfSignedDocumentReader.Read(tampered);

        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItFailsClosedOnADocumentItCannotRead()
    {
        var read = () => EcfSignedDocumentReader.Read("<ECF></ECF>");

        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItFailsClosedOnAnAmountItCannotRead()
    {
        var xml = new EcfXmlWriter().Write(Document())
            .Replace("<MontoItem>200.00</MontoItem>", "<MontoItem>no es un numero</MontoItem>");

        var read = () => EcfSignedDocumentReader.Read(xml);

        read.Should().Throw<InvalidOperationException>();
    }

    private static string Signed(EcfDocument document) => SignedXml(new EcfXmlWriter().Write(document));

    private static string SignedXml(string xml)
        => new XmlDsigEcfSigner().Sign(xml, TestCertificate.Create());

    private static EcfDocument Note() => Document() with
    {
        DocumentType = EcfDocumentType.DebitNote,
        Identity = new EcfIdentity
        {
            ENCF = "E330000000001",
            SequenceExpiresAt = new DateTime(2028, 12, 31),
            TypeIncome = EcfTypeIncome.Operations,
            PaymentType = EcfPaymentType.Cash
        },
        Reference = new EcfReference
        {
            ModifiedENCF = "E310000000009",
            OtherContributorTaxId = "130000002",
            ModifiedAt = new DateTime(2026, 11, 1),
            ModificationCode = EcfModificationCode.CorrectsAmounts,
            ModificationReason = "Correccion de los montos"
        }
    };

    private static EcfDocument Document(string? buyerTaxId = "130000002")
    {
        var items = new[]
        {
            new EcfItem
            {
                LineNumber = 1,
                TaxTreatment = TaxCode.ItbisTasa1,
                Name = "Café molido",
                Kind = EcfItemKind.Goods,
                Quantity = 2.5m,
                UnitPrice = 80m,
                Amount = 200m,
                DiscountAmount = 10m
            }
        };

        // Without a tax id of the buyer the document is a consumption
        // invoice: the credit one does not accept a buyer without a tax id.
        var documentType = buyerTaxId is null ? EcfDocumentType.Consumer : EcfDocumentType.FiscalCredit;

        return new EcfDocument
        {
            DocumentType = documentType,
            Identity = new EcfIdentity
            {
                ENCF = buyerTaxId is null ? "E320000000001" : "E310000000001",
                SequenceExpiresAt = buyerTaxId is null ? null : new DateTime(2028, 12, 31),
                TypeIncome = EcfTypeIncome.Operations,
                PaymentType = EcfPaymentType.Cash
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
                IssuedAt = new DateTime(2026, 11, 14)
            },
            Buyer = buyerTaxId is null
                ? new EcfBuyer { BusinessName = "Consumidor Final" }
                : new EcfBuyer { TaxId = buyerTaxId, BusinessName = "Cliente Corporativo SRL" },
            Totals = EcfTotalsCalculator.Calculate(items),
            Items = items,
            SignedAt = new DateTime(2026, 11, 14, 10, 30, 0)
        };
    }
}
