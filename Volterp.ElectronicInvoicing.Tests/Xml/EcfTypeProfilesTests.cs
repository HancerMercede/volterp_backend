using FluentAssertions;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Xml;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Xml;

public class EcfTypeProfilesTests
{
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
}
