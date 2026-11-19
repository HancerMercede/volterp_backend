using FluentAssertions;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Dgii;

// The document of the circuit is checked here, without the certificate and
// without the DGII: what the writer produces has to be a document the
// official schema accepts. Otherwise the verdict of the DGII would say
// about the shape of the document and not about the taxpayer.
public class TestEcfDocumentTests
{
    private const string IssuerRnc = "430359157";
    private const string Encf = "E310000000001";

    [Fact]
    public void TheDocumentOfTheCircuitIsAcceptedByTheOfficialSchema()
    {
        var act = () => TestEcfDocument.Build(IssuerRnc, Encf);

        act.Should().NotThrow(
            "the writer validates the document against the schema of the type: "
            + "a document that does not pass is a document the DGII rejects");
    }

    [Fact]
    public void TheDocumentCarriesTheIssuerAndTheNumberOfTheRange()
    {
        var xml = TestEcfDocument.Build(IssuerRnc, Encf);

        xml.Should().Contain($"<RNCEmisor>{IssuerRnc}</RNCEmisor>");
        xml.Should().Contain($"<eNCF>{Encf}</eNCF>");
    }
}
