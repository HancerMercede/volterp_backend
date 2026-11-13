using System.Xml;
using FluentAssertions;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.ElectronicInvoicing.Xml;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Xml;

// The summary of a consumption invoice follows the "Formato de Resumen Factura
// de Consumo Electrónica <DOP250,000": its areas, their order and its tags.
public class RfceXmlWriterTests
{
    private readonly RfceXmlWriter _writer = new();

    [Fact]
    public void Write_UsesTheRootAndTheVersionOfTheFormat()
    {
        var xml = _writer.Write(BuildDocument());

        xml.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?><RFCE><Encabezado>");
        xml.Should().EndWith("</Encabezado></RFCE>");
    }

    [Fact]
    public void Write_UsesTheAreasAndTheOrderOfTheFormat()
    {
        var xml = _writer.Write(BuildDocument());

        ElementsOf(xml).Should().ContainInOrder(
            "Version",
            "IdDoc", "TipoeCF", "eNCF", "TipoIngresos", "TipoPago",
            "TablaFormasPago", "FormaDePago", "FormaPago", "MontoPago",
            "Emisor", "RNCEmisor", "RazonSocialEmisor", "FechaEmision",
            "Comprador", "RNCComprador", "RazonSocialComprador",
            "Totales", "MontoGravadoTotal", "MontoGravadoI1", "MontoExento",
            "TotalITBIS", "TotalITBIS1", "MontoTotal", "MontoNoFacturable",
            "MontoPeriodo",
            "CodigoSeguridadeCF");
    }

    [Fact]
    public void Write_OmitsTheAmountsThatDoNotApply()
    {
        // The format makes most of the totals conditional: an amount that does
        // not apply is left out, not written as a zero.
        var document = BuildDocument() with
        {
            Totals = BuildDocument().Totals with
            {
                ExemptTotal = null,
                ItbisAtRate1 = null,
                NonBillableTotal = null
            }
        };

        var xml = _writer.Write(document);

        xml.Should().NotContain("MontoExento");
        xml.Should().NotContain("TotalITBIS1");
        xml.Should().NotContain("MontoNoFacturable");
        xml.Should().Contain("<MontoTotal>1180.00</MontoTotal>");
    }

    [Fact]
    public void Write_EmitsTheTypeOfTheConsumptionInvoice()
    {
        var xml = _writer.Write(BuildDocument());

        xml.Should().Contain("<TipoeCF>32</TipoeCF>");
    }

    [Fact]
    public void Write_CarriesTheSecurityCodeOfTheInvoiceItSummarizes()
    {
        var xml = _writer.Write(BuildDocument());

        xml.Should().Contain("<CodigoSeguridadeCF>BoXjC8</CodigoSeguridadeCF>");
    }

    [Fact]
    public void Write_FormatsTheAmountsWithTwoDecimalsAndTheDatesAsTheFormatSays()
    {
        var xml = _writer.Write(BuildDocument());

        xml.Should().Contain("<MontoGravadoTotal>1000.00</MontoGravadoTotal>");
        xml.Should().Contain("<MontoTotal>1180.00</MontoTotal>");
        xml.Should().Contain("<MontoPago>1180.00</MontoPago>");
        xml.Should().Contain("<FechaEmision>04-11-2026</FechaEmision>");
    }

    [Fact]
    public void Write_OmitsTheBuyerWhenTheInvoiceHasNone()
    {
        var document = BuildDocument() with { Buyer = null };

        var xml = _writer.Write(document);

        xml.Should().NotContain("<Comprador>");
    }

    [Fact]
    public void Write_KeepsTheDocumentFreeOfWhitespaceThatWouldBreakItsSignature()
    {
        var xml = _writer.Write(BuildDocument());

        xml.Should().NotContain("\n");
        xml.Should().NotContain("> <");
    }

    [Fact]
    public void Write_RefusesADocumentWithoutTheSecurityCode()
    {
        var document = BuildDocument() with { SecurityCode = " " };

        var act = () => _writer.Write(document);

        act.Should().Throw<EcfXmlException>().WithMessage("*CodigoSeguridadeCF*");
    }

    [Fact]
    public void TheSummaryIsSignedOverTheWholeDocument()
    {
        using var certificate = TestCertificate.Create();
        var signer = new XmlDsigEcfSigner();

        var signed = signer.Sign(_writer.Write(BuildDocument()), certificate);

        signed.Should().Contain("<Signature");
        signer.Verify(signed, certificate).Should().BeTrue();
    }

    [Fact]
    public void TheSecurityCodeOfTheSummaryItselfComesOutOfItsOwnSignature()
    {
        // The code of the summary cannot travel inside the summary: the tag is
        // part of what the signature covers. The one that travels is the code
        // of the invoice it summarizes.
        using var certificate = TestCertificate.Create();
        var signer = new XmlDsigEcfSigner();

        var signed = signer.Sign(_writer.Write(BuildDocument()), certificate);
        var codeOfTheSummary = EcfSecurityCode.From(signed);

        codeOfTheSummary.Should().HaveLength(6);
        signed.Should().NotContain($"<CodigoSeguridadeCF>{codeOfTheSummary}</CodigoSeguridadeCF>");
        signed.Should().Contain("<CodigoSeguridadeCF>BoXjC8</CodigoSeguridadeCF>");
    }

    private static RfceDocument BuildDocument() => new()
    {
        Identity = new RfceIdentity
        {
            ENCF = "E320000000064",
            TypeIncome = EcfTypeIncome.Operations,
            PaymentType = EcfPaymentType.Cash,
            Payments = [new RfcePayment { Method = EcfPaymentMethod.Cash, Amount = 1180m }]
        },
        Issuer = new RfceIssuer
        {
            TaxId = "131880738",
            BusinessName = "Emisor de Prueba SRL",
            IssuedAt = new DateTime(2026, 11, 4)
        },
        Buyer = new RfceBuyer
        {
            TaxId = "10100000999",
            BusinessName = "Cliente de Prueba SRL"
        },
        Totals = new RfceTotals
        {
            TaxableTotal = 1000m,
            TaxableAtRate1 = 1000m,
            ExemptTotal = 0m,
            ItbisTotal = 180m,
            ItbisAtRate1 = 180m,
            Total = 1180m,
            NonBillableTotal = 0m,
            PeriodTotal = 1180m
        },
        SecurityCode = "BoXjC8"
    };

    private static List<string> ElementsOf(string xml)
    {
        var elements = new List<string>();
        var reader = XmlReader.Create(new StringReader(xml));

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
                elements.Add(reader.Name);
        }

        return elements;
    }
}
