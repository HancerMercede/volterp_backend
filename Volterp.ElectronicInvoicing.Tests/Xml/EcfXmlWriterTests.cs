using FluentAssertions;
using Volterp.ElectronicInvoicing.Schemas;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.ElectronicInvoicing.Xml;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Xml;

public class EcfXmlWriterTests
{
    private readonly EcfXmlWriter _writer = new();

    [Fact]
    public void Write_ProducesADocumentThatMatchesTheOfficialSchema()
    {
        var xml = _writer.Write(BuildDocument(EcfDocumentType.FiscalCredit));

        var violations = EcfSchemas.For(EcfDocumentType.FiscalCredit).ValidateUnsigned(xml);

        violations.Should().BeEmpty();
    }

    [Theory]
    [InlineData(EcfDocumentType.FiscalCredit)]
    [InlineData(EcfDocumentType.Consumer)]
    [InlineData(EcfDocumentType.DebitNote)]
    [InlineData(EcfDocumentType.CreditNote)]
    [InlineData(EcfDocumentType.Purchase)]
    public void Write_ProducesADocumentThatMatchesTheOfficialSchemaOfEveryType(EcfDocumentType documentType)
    {
        var xml = _writer.Write(BuildDocument(documentType));

        var violations = EcfSchemas.For(documentType).ValidateUnsigned(xml);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Write_ProducesADocumentThatTheOfficialSchemaAcceptsOnceItIsSigned()
    {
        using var certificate = TestCertificate.Create();
        var signer = new XmlDsigEcfSigner();

        var xml = _writer.Write(BuildDocument(EcfDocumentType.FiscalCredit));
        var signed = signer.Sign(xml, certificate);

        var violations = EcfSchemas.For(EcfDocumentType.FiscalCredit).Validate(signed);

        violations.Should().BeEmpty();
        signed.Should().Contain("<Signature");
    }

    [Fact]
    public void Write_UsesTheRootAndTheVersionTheDgiiExpects()
    {
        var xml = _writer.Write(BuildDocument(EcfDocumentType.FiscalCredit));

        xml.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?><ECF>");
        xml.Should().Contain("<Version>1.0</Version>");
        xml.Should().EndWith("</ECF>");
    }

    [Fact]
    public void Write_UsesTheOfficialOrderOfTheAreas()
    {
        var xml = _writer.Write(BuildDocument(EcfDocumentType.FiscalCredit));

        var areas = AreasOf(xml);

        areas.Should().ContainInOrder(
            "Encabezado",
            "IdDoc",
            "Emisor",
            "Comprador",
            "Totales",
            "DetallesItems",
            "Item",
            "FechaHoraFirma");
    }

    [Fact]
    public void Write_EmitsTheTypeCodeOfTheDocument()
    {
        var xml = _writer.Write(BuildDocument(EcfDocumentType.Consumer));

        xml.Should().Contain("<TipoeCF>32</TipoeCF>");
    }

    [Fact]
    public void Write_FormatsTheDatesAsTheDgiiExpects()
    {
        var xml = _writer.Write(BuildDocument(EcfDocumentType.FiscalCredit));

        xml.Should().Contain("<FechaEmision>04-11-2026</FechaEmision>");
        xml.Should().Contain("<FechaVencimientoSecuencia>31-12-2026</FechaVencimientoSecuencia>");
        xml.Should().Contain("<FechaHoraFirma>04-11-2026 10:30:00</FechaHoraFirma>");
    }

    [Fact]
    public void Write_FormatsTheAmountsWithTwoDecimals()
    {
        var xml = _writer.Write(BuildDocument(EcfDocumentType.FiscalCredit));

        xml.Should().Contain("<MontoTotal>1180.00</MontoTotal>");
        xml.Should().Contain("<MontoGravadoTotal>1000.00</MontoGravadoTotal>");
        xml.Should().Contain("<MontoItem>1180.00</MontoItem>");
    }

    [Fact]
    public void Write_KeepsTheUnitPriceWithItsOwnPrecision()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Items = [BuildItem() with { UnitPrice = 99.999m, Amount = 1180m }]
        };

        var xml = _writer.Write(document);

        xml.Should().Contain("<PrecioUnitarioItem>99.999</PrecioUnitarioItem>");
    }

    [Fact]
    public void Write_OmitsWhatTheTypeOfTheDocumentDoesNotAccept()
    {
        var xml = _writer.Write(BuildDocument(EcfDocumentType.Consumer));

        xml.Should().NotContain("FechaVencimientoSecuencia");
    }

    [Fact]
    public void Write_OmitsTheBuyerWhenTheTypeDoesNotAcceptOne()
    {
        var document = BuildDocument(EcfDocumentType.CreditNote) with { Buyer = null };

        var xml = _writer.Write(document);

        xml.Should().NotContain("<Comprador>");
    }

    [Fact]
    public void Write_EmitsTheReferenceOfANote()
    {
        var document = BuildDocument(EcfDocumentType.CreditNote) with
        {
            Reference = new EcfReference
            {
                ModifiedENCF = "B310000000000",
                ModifiedAt = new DateTime(2026, 10, 1),
                ModificationCode = EcfModificationCode.CorrectsAmounts,
                ModificationReason = "error en el precio"
            }
        };

        var xml = _writer.Write(document);

        xml.Should().Contain("<NCFModificado>B310000000000</NCFModificado>");
        xml.Should().Contain("<FechaNCFModificado>01-10-2026</FechaNCFModificado>");
        xml.Should().Contain("<CodigoModificacion>3</CodigoModificacion>");
        xml.Should().Contain("<RazonModificacion>error en el precio</RazonModificacion>");
    }

    [Fact]
    public void Write_EmitsTheCreditNoteIndicatorTheTypeRequires()
    {
        var document = BuildDocument(EcfDocumentType.CreditNote) with
        {
            Identity = BuildIdentity() with { CreditNoteBeyondThirtyDays = true }
        };

        var xml = _writer.Write(document);

        xml.Should().Contain("<IndicadorNotaCredito>1</IndicadorNotaCredito>");
    }

    [Fact]
    public void Write_EmitsThePaymentFormsAndTheAccountTheyComeFrom()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Identity = BuildIdentity() with
            {
                PaymentType = EcfPaymentType.Credit,
                PaymentDueDate = new DateTime(2026, 12, 4),
                PaymentTerm = "30 días",
                Payments = [new EcfPayment { Method = EcfPaymentMethod.Cash, Amount = 500m }],
                PaymentAccount = new EcfPaymentAccount
                {
                    Type = EcfAccountType.Current,
                    Number = "12345678901",
                    Bank = "BHD"
                }
            }
        };

        var xml = _writer.Write(document);

        xml.Should().Contain("<TipoPago>2</TipoPago>");
        xml.Should().Contain("<TerminoPago>30 días</TerminoPago>");
        xml.Should().Contain("<FormaPago>1</FormaPago>");
        xml.Should().Contain("<MontoPago>500.00</MontoPago>");
        xml.Should().Contain("<TipoCuentaPago>CT</TipoCuentaPago>");
        xml.Should().Contain("<BancoPago>BHD</BancoPago>");
    }

    [Fact]
    public void Write_ThrowsWhenTheTypeRequiresABuyerAndTheDocumentHasNone()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with { Buyer = null };

        var act = () => _writer.Write(document);

        act.Should().Throw<EcfXmlException>().WithMessage("*Comprador*");
    }

    [Fact]
    public void Write_ThrowsWhenTheTypeRequiresAReferenceAndTheDocumentHasNone()
    {
        var document = BuildDocument(EcfDocumentType.CreditNote) with { Reference = null };

        var act = () => _writer.Write(document);

        act.Should().Throw<EcfXmlException>().WithMessage("*InformacionReferencia*");
    }

    [Fact]
    public void Write_ThrowsWhenTheDocumentDoesNotMatchTheOfficialSchema()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Issuer = BuildIssuer() with { TaxId = "123" }
        };

        var act = () => _writer.Write(document);

        act.Should().Throw<EcfXmlException>()
            .Which.Violations.Should().NotBeEmpty();
    }

    [Fact]
    public void Write_KeepsTheDocumentFreeOfWhitespaceThatWouldBreakItsSignature()
    {
        var xml = _writer.Write(BuildDocument(EcfDocumentType.FiscalCredit));

        xml.Should().NotContain("\n");
        xml.Should().NotContain("> <");
    }

    private static EcfDocument BuildDocument(EcfDocumentType documentType) => new()
    {
        DocumentType = documentType,
        Identity = BuildIdentity(),
        Issuer = BuildIssuer(),
        Buyer = documentType is EcfDocumentType.CreditNote or EcfDocumentType.DebitNote
            ? null
            : new EcfBuyer
            {
                TaxId = "10100000999",
                BusinessName = "Cliente de Prueba SRL"
            },
        Totals = new EcfTotals
        {
            TaxableTotal = 1000m,
            TaxableAtRate1 = 1000m,
            ItbisRate1 = 18,
            ItbisTotal = 180m,
            ItbisAtRate1 = 180m,
            Total = 1180m
        },
        Items = [BuildItem() with
        {
            // The purchases type demands the retention block on every line.
            Retention = documentType is EcfDocumentType.Purchase
                ? new EcfRetention { IsWithholdingAgent = true }
                : null
        }],
        Reference = documentType is EcfDocumentType.CreditNote or EcfDocumentType.DebitNote
            ? new EcfReference
            {
                ModifiedENCF = "B310000000000",
                ModifiedAt = new DateTime(2026, 10, 1),
                ModificationCode = EcfModificationCode.CorrectsAmounts
            }
            : null,
        SignedAt = new DateTime(2026, 11, 4, 10, 30, 0)
    };

    private static EcfIdentity BuildIdentity() => new()
    {
        ENCF = "B310000000000",
        SequenceExpiresAt = new DateTime(2026, 12, 31),
        TypeIncome = EcfTypeIncome.Operations,
        PaymentType = EcfPaymentType.Cash,
        CreditNoteBeyondThirtyDays = false
    };

    private static EcfIssuer BuildIssuer() => new()
    {
        TaxId = "10100000101",
        BusinessName = "Emisor de Prueba SRL",
        Address = "Calle Principal 1, Santo Domingo",
        IssuedAt = new DateTime(2026, 11, 4)
    };

    private static EcfItem BuildItem() => new()
    {
        LineNumber = 1,
        TaxTreatment = TaxCode.ItbisTasa1,
        Name = "Producto de prueba",
        Kind = EcfItemKind.Goods,
        Quantity = 1m,
        UnitPrice = 1000m,
        Amount = 1180m
    };

    private static List<string> AreasOf(string xml)
    {
        var areas = new List<string>();
        var reader = System.Xml.XmlReader.Create(new StringReader(xml));
        while (reader.Read())
        {
            if (reader.NodeType == System.Xml.XmlNodeType.Element && reader.Depth <= 2)
                areas.Add(reader.Name);
        }

        return areas;
    }
}
