using FluentAssertions;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Schemas;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Schemas;

public class EcfSchemaTests
{
    private const string TypeIncomePath = "Encabezado/IdDoc/TipoIngresos";

    [Theory]
    [InlineData(EcfDocumentType.FiscalCredit)]
    [InlineData(EcfDocumentType.Consumer)]
    [InlineData(EcfDocumentType.DebitNote)]
    [InlineData(EcfDocumentType.CreditNote)]
    [InlineData(EcfDocumentType.SpecialRegimes)]
    [InlineData(EcfDocumentType.Governmental)]
    [InlineData(EcfDocumentType.Exportation)]
    public void AllowedValues_ReturnsTheCodesTheSchemaEnumerates(EcfDocumentType documentType)
    {
        var values = EcfSchemas.For(documentType).AllowedValues(TypeIncomePath);

        values.Should().Equal("01", "02", "03", "04", "05", "06");
    }

    [Theory]
    [InlineData(EcfDocumentType.Purchase)]
    [InlineData(EcfDocumentType.MinorExpenses)]
    [InlineData(EcfDocumentType.ExternalPayments)]
    public void AllowedValues_IsEmptyForATypeThatDoesNotDeclareTheField(EcfDocumentType documentType)
    {
        var schema = EcfSchemas.For(documentType);

        schema.Allows(TypeIncomePath).Should().BeFalse();
        schema.AllowedValues(TypeIncomePath).Should().BeEmpty();
    }

    [Fact]
    public void AllowedValues_IsEmptyForAFieldTheTypeDoesNotHave()
    {
        var values = EcfSchemas.For(EcfDocumentType.FiscalCredit).AllowedValues("Encabezado/IdDoc/CampoQueNoExiste");

        values.Should().BeEmpty();
    }
}
