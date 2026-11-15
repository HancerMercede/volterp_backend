using System.Xml.Linq;
using FluentAssertions;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Schemas;
using Volterp.ElectronicInvoicing.Xml;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Xml;

public class EcfTypeProfilesTests
{
    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";

    [Fact]
    public void For_FiscalCredit_RequiresTheSequenceExpiryAndTheIdentityOfTheBuyer()
    {
        var profile = EcfTypeProfiles.For(EcfDocumentType.FiscalCredit);

        profile.RequiresSequenceExpiry.Should().BeTrue();
        profile.RequiresBuyer.Should().BeTrue();
        profile.RequiresBuyerTaxId.Should().BeTrue();
        profile.RequiresBuyerName.Should().BeTrue();
    }

    [Fact]
    public void For_Consumer_RequiresTheBuyerButNotItsIdentity()
    {
        var profile = EcfTypeProfiles.For(EcfDocumentType.Consumer);

        profile.RequiresSequenceExpiry.Should().BeFalse();
        profile.RequiresBuyer.Should().BeTrue();
        profile.RequiresBuyerTaxId.Should().BeFalse();
        profile.RequiresBuyerName.Should().BeFalse();
    }

    [Fact]
    public void For_UsesTheIncomeAndThePaymentThatFitASale()
    {
        var profile = EcfTypeProfiles.For(EcfDocumentType.Consumer);

        profile.TypeIncome.Should().Be(EcfTypeIncome.Operations);
        profile.PaymentType.Should().Be(EcfPaymentType.Cash);
        profile.ItemKind.Should().Be(EcfItemKind.Goods);
    }

    [Fact]
    public void For_KeepsTheTypeItWasAskedAbout()
    {
        var profile = EcfTypeProfiles.For(EcfDocumentType.FiscalCredit);

        profile.DocumentType.Should().Be(EcfDocumentType.FiscalCredit);
    }

    [Theory]
    [InlineData(EcfDocumentType.FiscalCredit)]
    [InlineData(EcfDocumentType.Consumer)]
    [InlineData(EcfDocumentType.DebitNote)]
    [InlineData(EcfDocumentType.CreditNote)]
    [InlineData(EcfDocumentType.Purchase)]
    [InlineData(EcfDocumentType.MinorExpenses)]
    [InlineData(EcfDocumentType.SpecialRegimes)]
    [InlineData(EcfDocumentType.Governmental)]
    [InlineData(EcfDocumentType.Exportation)]
    [InlineData(EcfDocumentType.ExternalPayments)]
    public void For_AcceptsExactlyTheIncomesTheSchemaOfTheTypeEnumerates(EcfDocumentType documentType)
    {
        var profile = EcfTypeProfiles.For(documentType);

        var codes = profile.AllowedTypeIncomes.Select(income => EcfTypeIncomes.Code(income));

        codes.Should().Equal(IncomeCodesOfTheSchema(documentType));
    }

    [Theory]
    [InlineData(EcfDocumentType.Purchase)]
    [InlineData(EcfDocumentType.MinorExpenses)]
    [InlineData(EcfDocumentType.ExternalPayments)]
    public void For_LeavesTheIncomeEmptyForTheTypesWhoseSchemaDoesNotDeclareIt(EcfDocumentType documentType)
    {
        var profile = EcfTypeProfiles.For(documentType);

        profile.TypeIncome.Should().BeNull();
        profile.AllowedTypeIncomes.Should().BeEmpty();
    }

    [Theory]
    [InlineData(EcfDocumentType.FiscalCredit)]
    [InlineData(EcfDocumentType.Consumer)]
    [InlineData(EcfDocumentType.DebitNote)]
    [InlineData(EcfDocumentType.CreditNote)]
    [InlineData(EcfDocumentType.SpecialRegimes)]
    [InlineData(EcfDocumentType.Governmental)]
    [InlineData(EcfDocumentType.Exportation)]
    public void For_DerivesTheFirstIncomeTheSchemaOfTheTypeEnumerates(EcfDocumentType documentType)
    {
        var profile = EcfTypeProfiles.For(documentType);

        profile.TypeIncome.Should().Be(EcfTypeIncome.Operations);
        profile.AllowedTypeIncomes.Should().Equal(
            EcfTypeIncome.Operations,
            EcfTypeIncome.Financial,
            EcfTypeIncome.Extraordinary,
            EcfTypeIncome.Leases,
            EcfTypeIncome.DepreciableAssetSale,
            EcfTypeIncome.Other);
    }

    // The codes the schema of the type enumerates, read from the schema file
    // itself, so the derivation is compared against the source and not against
    // another reading of it.
    private static IReadOnlyList<string> IncomeCodesOfTheSchema(EcfDocumentType documentType)
    {
        var assembly = typeof(EcfSchemas).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith($"ecf-{(int)documentType}.xsd", StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resource)!;
        var schema = XDocument.Load(stream);

        var element = schema.Descendants(Xsd + "element")
            .FirstOrDefault(candidate => (string?)candidate.Attribute("name") == "TipoIngresos"
                && candidate.Ancestors(Xsd + "element").Any(ancestor => (string?)ancestor.Attribute("name") == "IdDoc"));

        if (element is null)
            return [];

        var typeName = (string?)element.Attribute("type");
        var simpleType = schema.Descendants(Xsd + "simpleType")
            .FirstOrDefault(candidate => (string?)candidate.Attribute("name") == typeName);

        return simpleType?.Descendants(Xsd + "enumeration")
            .Select(enumeration => (string?)enumeration.Attribute("value") ?? string.Empty)
            .ToList() ?? [];
    }
}
