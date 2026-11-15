using FluentAssertions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Qr;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Qr;

// The timbre of a document that is not a summary: the URL the QR of its
// printed representation carries, on the host of the taxpayer
// ("Descripción Técnica Servicios DGII", p. 40-41).
public class EcfTimbreUrlTests
{
    [Fact]
    public void ForTimbre_UsesTheContractOfTheDescription()
    {
        var url = EcfVerificationUrl.ForTimbre(
            DgiiEnvironment.PreCertification,
            "130000001",
            "130000002",
            "e310000000001",
            2.11m,
            new DateTime(2020, 10, 10),
            new DateTime(2020, 10, 10, 9, 0, 0),
            "dcp79q");

        url.Should().Be(
            "https://ecf.dgii.gov.do/testecf/consultatimbre"
            + "?rncemisor=130000001"
            + "&rnccomprador=130000002"
            + "&encf=e310000000001"
            + "&fechaemision=10-10-2020"
            + "&montototal=2.11"
            + "&fechafirma=10-10-2020%2009:00:00"
            + "&codigoseguridad=dcp79q");
    }

    [Fact]
    public void ForTimbre_UsesTheEnvironmentOfTheDocument()
    {
        var production = EcfVerificationUrl.ForTimbre(
            DgiiEnvironment.Production,
            "130000001",
            "130000002",
            "e310000000001",
            2.11m,
            new DateTime(2020, 10, 10),
            new DateTime(2020, 10, 10, 9, 0, 0),
            "dcp79q");

        var certification = EcfVerificationUrl.ForTimbre(
            DgiiEnvironment.Certification,
            "130000001",
            "130000002",
            "e310000000001",
            2.11m,
            new DateTime(2020, 10, 10),
            new DateTime(2020, 10, 10, 9, 0, 0),
            "dcp79q");

        production.Should().StartWith("https://ecf.dgii.gov.do/ecf/consultatimbre");
        certification.Should().StartWith("https://ecf.dgii.gov.do/certecf/consultatimbre");
    }

    [Fact]
    public void ForTimbre_OfADocumentWithoutABuyer_KeepsTheEmptyBuyer()
    {
        var url = EcfVerificationUrl.ForTimbre(
            DgiiEnvironment.PreCertification,
            "130000001",
            null,
            "e310000000001",
            2.11m,
            new DateTime(2020, 10, 10),
            new DateTime(2020, 10, 10, 9, 0, 0),
            "dcp79q");

        url.Should().Contain("&rnccomprador=&");
    }
}
