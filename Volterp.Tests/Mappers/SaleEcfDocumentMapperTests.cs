using FluentAssertions;
using Volterp.Application.Mappers;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Schemas;
using Volterp.ElectronicInvoicing.Xml;
using Xunit;

namespace Volterp.Tests.Mappers;

public class SaleEcfDocumentMapperTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    private readonly EcfXmlWriter _writer = new();

    [Fact]
    public void Map_TakesTheLinesOfTheSaleOneByOne()
    {
        var sale = Sale();
        sale.AddItem(Guid.NewGuid(), "Café molido", 2, 100m, TaxCode.ItbisTasa1);
        sale.AddItem(Guid.NewGuid(), "Pan integral", 1, 50m, TaxCode.Exento);

        var document = SaleEcfDocumentMapper.Map(sale, Company(), Issuance());

        document.Items.Should().HaveCount(2);
        document.Items[0].LineNumber.Should().Be(1);
        document.Items[0].Name.Should().Be("Café molido");
        document.Items[0].TaxTreatment.Should().Be(TaxCode.ItbisTasa1);
        document.Items[0].Quantity.Should().Be(2m);
        document.Items[0].UnitPrice.Should().Be(100m);
        document.Items[0].Amount.Should().Be(200m);
        document.Items[1].LineNumber.Should().Be(2);
        document.Items[1].TaxTreatment.Should().Be(TaxCode.Exento);
    }

    [Fact]
    public void Map_SplitsTheTotalsByTaxRate()
    {
        var sale = Sale();
        sale.AddItem(Guid.NewGuid(), "Café molido", 1, 1000m, TaxCode.ItbisTasa1);
        sale.AddItem(Guid.NewGuid(), "Licuadora", 1, 500m, TaxCode.ItbisTasa2);
        sale.AddItem(Guid.NewGuid(), "Pan integral", 1, 100m, TaxCode.Exento);

        var totals = SaleEcfDocumentMapper.Map(sale, Company(), Issuance()).Totals;

        totals.TaxableTotal.Should().Be(1500m);
        totals.TaxableAtRate1.Should().Be(1000m);
        totals.TaxableAtRate2.Should().Be(500m);
        totals.ExemptTotal.Should().Be(100m);
        totals.ItbisRate1.Should().Be(18);
        totals.ItbisRate2.Should().Be(16);
        totals.ItbisAtRate1.Should().Be(180m);
        totals.ItbisAtRate2.Should().Be(80m);
        totals.ItbisTotal.Should().Be(260m);
        totals.Total.Should().Be(1860m);
    }

    [Fact]
    public void Map_UsesTheCompanyAsTheIssuer()
    {
        var document = SaleEcfDocumentMapper.Map(SaleWithOneLine(), Company(), Issuance());

        document.Issuer.TaxId.Should().Be("10100000101");
        document.Issuer.BusinessName.Should().Be("Distribuidora del Norte SRL");
        document.Issuer.Address.Should().Be("Calle Principal 1, Santo Domingo");
    }

    [Fact]
    public void Map_UsesTheSaleAsTheBuyer()
    {
        var sale = SaleWithOneLine();
        sale.ReceptorTaxId = "101010101";
        sale.ClienteName = "Cliente de Prueba SRL";

        var document = SaleEcfDocumentMapper.Map(sale, Company(), Issuance());

        document.Buyer.Should().NotBeNull();
        document.Buyer!.TaxId.Should().Be("101010101");
        document.Buyer.BusinessName.Should().Be("Cliente de Prueba SRL");
    }

    [Fact]
    public void Map_KeepsTheNumberAndTheMomentOfTheDocument()
    {
        var issuance = Issuance(new DateTime(2026, 12, 31));

        var document = SaleEcfDocumentMapper.Map(SaleWithOneLine(), Company(), issuance);

        document.Identity.ENCF.Should().Be("B310000000000");
        document.Identity.SequenceExpiresAt.Should().Be(new DateTime(2026, 12, 31));
        document.Identity.TypeIncome.Should().Be(EcfTypeIncome.Operations);
        document.Identity.PaymentType.Should().Be(EcfPaymentType.Cash);
        document.SignedAt.Should().Be(new DateTime(2026, 11, 4, 10, 30, 0));
    }

    [Fact]
    public void Map_ProducesADocumentTheOfficialSchemaAccepts()
    {
        var sale = Sale();
        sale.DocumentType = EcfDocumentType.FiscalCredit;
        sale.ReceptorTaxId = "101010101";
        sale.ClienteName = "Cliente de Prueba SRL";
        sale.AddItem(Guid.NewGuid(), "Café molido", 1, 1000m, TaxCode.ItbisTasa1);

        var document = SaleEcfDocumentMapper.Map(sale, Company(), Issuance(new DateTime(2026, 12, 31)));

        var violations = EcfSchemas.For(EcfDocumentType.FiscalCredit).ValidateUnsigned(_writer.Write(document));

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Map_ProducesAConsumerInvoiceTheOfficialSchemaAccepts()
    {
        var sale = Sale();
        sale.AddItem(Guid.NewGuid(), "Café molido", 1, 1000m, TaxCode.ItbisTasa1);

        var document = SaleEcfDocumentMapper.Map(sale, Company(), Issuance());

        var violations = EcfSchemas.For(EcfDocumentType.Consumer).ValidateUnsigned(_writer.Write(document));

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Map_EmitsTheAmountOfEveryLineBeforeTheTaxes()
    {
        var sale = Sale();
        sale.AddItem(Guid.NewGuid(), "Café molido", 1, 1000m, TaxCode.ItbisTasa1);
        sale.AddItem(Guid.NewGuid(), "Licuadora", 1, 500m, TaxCode.ItbisTasa2);

        var xml = _writer.Write(SaleEcfDocumentMapper.Map(sale, Company(), Issuance()));

        xml.Should().Contain("<MontoItem>1000.00</MontoItem>");
        xml.Should().Contain("<MontoItem>500.00</MontoItem>");
        xml.Should().Contain("<MontoGravadoTotal>1500.00</MontoGravadoTotal>");
        xml.Should().Contain("<MontoGravadoI1>1000.00</MontoGravadoI1>");
        xml.Should().Contain("<MontoGravadoI2>500.00</MontoGravadoI2>");
        xml.Should().Contain("<TotalITBIS>260.00</TotalITBIS>");
        xml.Should().Contain("<MontoTotal>1760.00</MontoTotal>");
    }

    [Fact]
    public void Map_ThrowsWhenTheTypeRequiresTheSequenceExpiryAndThereIsNone()
    {
        var sale = Sale();
        sale.DocumentType = EcfDocumentType.FiscalCredit;
        sale.ReceptorTaxId = "101010101";
        sale.AddItem(Guid.NewGuid(), "Café molido", 1, 1000m, TaxCode.ItbisTasa1);

        var act = () => SaleEcfDocumentMapper.Map(sale, Company(), Issuance());

        act.Should().Throw<InvalidOperationException>().WithMessage("*sequence*");
    }

    [Fact]
    public void Map_ThrowsWhenTheTypeRequiresTheTaxIdOfTheBuyerAndThereIsNone()
    {
        var sale = Sale();
        sale.DocumentType = EcfDocumentType.FiscalCredit;
        sale.AddItem(Guid.NewGuid(), "Café molido", 1, 1000m, TaxCode.ItbisTasa1);

        var act = () => SaleEcfDocumentMapper.Map(sale, Company(), Issuance(new DateTime(2026, 12, 31)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*tax id*");
    }

    [Fact]
    public void Map_ThrowsWhenTheSaleHasNoLines()
    {
        var act = () => SaleEcfDocumentMapper.Map(Sale(), Company(), Issuance());

        act.Should().Throw<InvalidOperationException>().WithMessage("*line*");
    }

    [Fact]
    public void Map_ThrowsWhenTheSaleDoesNotBelongToTheCompanyThatIssuesIt()
    {
        var otherCompany = Company();
        otherCompany.Id = Guid.NewGuid();

        var act = () => SaleEcfDocumentMapper.Map(SaleWithOneLine(), otherCompany, Issuance());

        act.Should().Throw<InvalidOperationException>().WithMessage("*company*");
    }

    private static Sale Sale() => new()
    {
        CompanyId = CompanyId,
        DocumentType = EcfDocumentType.Consumer,
        CreatedAt = new DateTime(2026, 11, 4)
    };

    private static Sale SaleWithOneLine()
    {
        var sale = Sale();
        sale.AddItem(Guid.NewGuid(), "Café molido", 1, 1000m, TaxCode.ItbisTasa1);
        return sale;
    }

    private static Company Company() => new()
    {
        Id = CompanyId,
        Name = "Distribuidora del Norte",
        LegalName = "Distribuidora del Norte SRL",
        TaxId = "10100000101",
        Address = "Calle Principal 1, Santo Domingo",
        Phone = "809-555-1234",
        Email = "facturacion@distribuidora.do"
    };

    private static EcfIssuance Issuance(DateTime? sequenceExpiresAt = null) => new()
    {
        ENCF = "B310000000000",
        SequenceExpiresAt = sequenceExpiresAt,
        SignedAt = new DateTime(2026, 11, 4, 10, 30, 0)
    };
}
