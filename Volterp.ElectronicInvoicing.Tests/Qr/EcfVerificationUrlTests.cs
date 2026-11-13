using FluentAssertions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Qr;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Qr;

// The verification URLs are the contract with the DGII, so every test here
// cites the page of the "Descripción Técnica Servicios DGII" that fixes it.
public class EcfVerificationUrlTests
{
    [Fact]
    public void ForConsumerInquiry_UsesTheContractOfTheDescription()
    {
        // p. 20: GET https://fc.dgii.gov.do/ecf/consultarfce/api/Consultas/Consulta
        //      ?RNC_Emisor=111111111&ENCF=E320000000000&Cod_Seguridad_eCF=XXXXXX
        var url = EcfVerificationUrl.ForConsumerInquiry(
            DgiiEnvironment.Production, "111111111", "E320000000000", "BoXjC8");

        url.Should().Be(
            "https://fc.dgii.gov.do/ecf/consultarfce/api/Consultas/Consulta"
            + "?RNC_Emisor=111111111&ENCF=E320000000000&Cod_Seguridad_eCF=BoXjC8");
    }

    [Fact]
    public void ForRfceTimbre_UsesTheContractOfTheDescription()
    {
        // p. 42: https://fc.dgii.gov.do/testecf/consultatimbrefc
        //      ?rncemisor=131880738&encf=...&montototal=6225.09&codigoseguridad=...
        var url = EcfVerificationUrl.ForRfceTimbre(
            DgiiEnvironment.PreCertification, "131880738", "E320000000064", 6225.09m, "uabnyh");

        url.Should().Be(
            "https://fc.dgii.gov.do/testecf/consultatimbrefc"
            + "?rncemisor=131880738&encf=E320000000064&montototal=6225.09&codigoseguridad=uabnyh");
    }

    [Theory]
    [InlineData(DgiiEnvironment.PreCertification, "testecf")]
    [InlineData(DgiiEnvironment.Certification, "certecf")]
    [InlineData(DgiiEnvironment.Production, "ecf")]
    public void TheEnvironmentIsTheSegmentOfTheUrl(DgiiEnvironment environment, string segment)
    {
        var inquiry = EcfVerificationUrl.ForConsumerInquiry(
            environment, "131880738", "E320000000064", "BoXjC8");

        var timbre = EcfVerificationUrl.ForRfceTimbre(
            environment, "131880738", "E320000000064", 6225.09m, "BoXjC8");

        inquiry.Should().Contain($"/{segment}/");
        timbre.Should().Contain($"/{segment}/");
    }

    [Fact]
    public void TheSummariesTravelOnTheConsumptionHostAndNotOnTheTaxpayerOne()
    {
        var url = EcfVerificationUrl.ForRfceTimbre(
            DgiiEnvironment.Production, "131880738", "E320000000064", 6225.09m, "BoXjC8");

        url.Should().StartWith("https://fc.dgii.gov.do/");
        url.Should().NotContain("ecf.dgii.gov.do");
    }

    [Fact]
    public void TheSecurityCodeTravelsUrlEncoded()
    {
        // The code comes out of a base64 signature, so it carries characters
        // that would break the query if they travelled raw.
        var url = EcfVerificationUrl.ForRfceTimbre(
            DgiiEnvironment.Production, "131880738", "E320000000064", 6225.09m, "Buc+Mq7");

        url.Should().Contain("codigoseguridad=Buc%2BMq7");
        url.Should().NotContain("Buc+Mq7");
    }

    [Fact]
    public void TheSameInputsAlwaysGiveTheSameUrl()
    {
        var first = EcfVerificationUrl.ForRfceTimbre(
            DgiiEnvironment.Production, "131880738", "E320000000064", 6225.09m, "BoXjC8");

        var second = EcfVerificationUrl.ForRfceTimbre(
            DgiiEnvironment.Production, "131880738", "E320000000064", 6225.09m, "BoXjC8");

        first.Should().Be(second);
    }

    [Fact]
    public void TheAmountTravelsWithTheTwoDecimalsTheFormatUses()
    {
        var url = EcfVerificationUrl.ForRfceTimbre(
            DgiiEnvironment.Production, "131880738", "E320000000064", 180000m, "BoXjC8");

        url.Should().Contain("montototal=180000.00");
    }

    [Fact]
    public void ForConsumerInquiry_RefusesAnEmptySecurityCode()
    {
        var act = () => EcfVerificationUrl.ForConsumerInquiry(
            DgiiEnvironment.Production, "131880738", "E320000000064", " ");

        act.Should().Throw<ArgumentException>();
    }
}
