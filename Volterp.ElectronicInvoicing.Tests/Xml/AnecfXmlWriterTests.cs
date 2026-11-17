using System.Net;
using FluentAssertions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Schemas;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.ElectronicInvoicing.Tests.Dgii;
using Volterp.ElectronicInvoicing.Xml;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Xml;

// The document that announces to the DGII the e-NCF numbers a range will
// never use: the "Formato Anulación de Secuencias de e-NCF v1.0".
public class AnecfXmlWriterTests
{
    private readonly AnecfXmlWriter _writer = new();

    [Fact]
    public void Write_ProducesADocumentThatMatchesTheOfficialSchema()
    {
        var xml = _writer.Write(BuildDocument());

        AnecfSchema.Instance.ValidateUnsigned(xml).Should().BeEmpty();
    }

    [Fact]
    public void Write_UsesTheRootAndTheVersionOfTheFormat()
    {
        var xml = _writer.Write(BuildDocument());

        xml.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?><ANECF>");
        xml.Should().EndWith("</ANECF>");
    }

    [Fact]
    public void Write_CarriesTheIssuerTheCountAndTheDayOfTheAnnulment()
    {
        var xml = _writer.Write(BuildDocument());

        xml.Should().Contain("<Version>1.0</Version>");
        xml.Should().Contain("<RncEmisor>10100000101</RncEmisor>");
        xml.Should().Contain("<CantidadeNCFAnulados>5</CantidadeNCFAnulados>");
        xml.Should().Contain("<FechaHoraAnulacioneNCF>04-11-2026 10:30:00</FechaHoraAnulacioneNCF>");
    }

    [Fact]
    public void Write_CarriesTheTwoFullEncfOfTheRange()
    {
        var xml = _writer.Write(BuildDocument());

        xml.Should().Contain("<SecuenciaeNCFDesde>E310000000006</SecuenciaeNCFDesde>");
        xml.Should().Contain("<SecuenciaeNCFHasta>E310000000010</SecuenciaeNCFHasta>");
    }

    [Fact]
    public void Write_NumbersTheLinesOfTheAnnulmentAndTheTypeOfTheRange()
    {
        var xml = _writer.Write(BuildDocument());

        xml.Should().Contain("<NoLinea>1</NoLinea>");
        xml.Should().Contain("<TipoeCF>31</TipoeCF>");
    }

    [Fact]
    public void Write_CountsTheSequencesOfEveryRange()
    {
        var document = BuildDocument() with
        {
            Annulments =
            [
                BuildAnnulment() with
                {
                    Sequences =
                    [
                        new AnecfSequenceRange { From = "E310000000006", To = "E310000000010" },
                        new AnecfSequenceRange { From = "E310000000020", To = "E310000000022" }
                    ]
                }
            ]
        };

        var xml = _writer.Write(document);

        xml.Should().Contain("<CantidadeNCFAnulados>8</CantidadeNCFAnulados>");
    }

    [Fact]
    public void Write_KeepsTheDocumentFreeOfWhitespaceThatWouldBreakItsSignature()
    {
        var xml = _writer.Write(BuildDocument());

        xml.Should().NotContain("\n");
        xml.Should().NotContain("> <");
    }

    [Fact]
    public void Write_ThrowsWhenTheRangeEndsBeforeItStarts()
    {
        var document = BuildDocument() with
        {
            Annulments =
            [
                BuildAnnulment() with
                {
                    Sequences = [new AnecfSequenceRange { From = "E310000000010", To = "E310000000006" }]
                }
            ]
        };

        var act = () => _writer.Write(document);

        act.Should().Throw<EcfXmlException>().WithMessage("*SecuenciaeNCFDesde*");
    }

    [Fact]
    public void Write_ThrowsWhenTheEncfIsNotOfTheTypeOfTheLine()
    {
        var document = BuildDocument() with
        {
            Annulments =
            [
                BuildAnnulment() with
                {
                    Sequences = [new AnecfSequenceRange { From = "E320000000006", To = "E320000000010" }]
                }
            ]
        };

        var act = () => _writer.Write(document);

        act.Should().Throw<EcfXmlException>().WithMessage("*31*");
    }

    [Fact]
    public void Write_ThrowsWhenTheDocumentDoesNotMatchTheOfficialSchema()
    {
        var document = BuildDocument() with { IssuerTaxId = "123" };

        var act = () => _writer.Write(document);

        act.Should().Throw<EcfXmlException>()
            .Which.Violations.Should().NotBeEmpty();
    }

    [Fact]
    public void TheAnnulmentIsSignedOverTheWholeDocument()
    {
        using var certificate = TestCertificate.Create();
        var signer = new XmlDsigEcfSigner();

        var signed = signer.Sign(_writer.Write(BuildDocument()), certificate);

        signed.Should().Contain("<Signature");
        signer.Verify(signed, certificate).Should().BeTrue();
        AnecfSchema.Instance.Validate(signed).Should().BeEmpty();
    }

    private static AnecfDocument BuildDocument() => new()
    {
        IssuerTaxId = "10100000101",
        GeneratedAt = new DateTime(2026, 11, 4, 10, 30, 0),
        Annulments = [BuildAnnulment()]
    };

    private static AnecfAnnulment BuildAnnulment() => new()
    {
        DocumentType = EcfDocumentType.FiscalCredit,
        Sequences = [new AnecfSequenceRange { From = "E310000000006", To = "E310000000010" }]
    };
}

// The transport of the annulment: the path of the service, the multipart
// field, the bearer token and the answer of the DGII.
public class AnecfDgiiClientTests
{
    [Fact]
    public async Task AnnulRangesAsync_PostsTheAnnulmentToThePathOfTheService()
    {
        var handler = StubHttpMessageHandler.ReturningJson(Answer());
        var client = CreateClient(handler);

        await client.AnnulRangesAsync(SignedAnecf(), "jwt");

        // "Descripción Técnica Servicios DGII", p. 35:
        // POST {base}/{ambiente}/anulacionrangos/api/operaciones/anularrango
        handler.LastMethod.Should().Be("POST");
        handler.LastUri.Should().Be(
            "https://ecf.dgii.gov.do/testecf/anulacionrangos/api/operaciones/anularrango");
        handler.LastAuthorization.Should().Be("bearer jwt");
        handler.LastContentType.Should().StartWith("multipart/form-data");
        handler.LastBody.Should().MatchRegex("name=\"?xml\"?");
        handler.LastBody.Should().Contain("text/xml");
        handler.LastBody.Should().Contain("<ANECF>");
    }

    [Fact]
    public async Task AnnulRangesAsync_NamesTheFileLikeTheSeed()
    {
        var handler = StubHttpMessageHandler.ReturningJson(Answer());
        var client = CreateClient(handler);

        await client.AnnulRangesAsync(SignedAnecf(), "jwt");

        // The annulment has no e-NCF of its own to name the file with, so it
        // takes the shape of the seed.
        handler.LastBody.Should().MatchRegex("filename=\"?response_");
    }

    [Fact]
    public async Task AnnulRangesAsync_ReturnsTheAnswerOfTheDgii()
    {
        var handler = StubHttpMessageHandler.ReturningJson(Answer());
        var client = CreateClient(handler);

        var answer = await client.AnnulRangesAsync(SignedAnecf(), "jwt");

        answer.Rnc.Should().Be("10100000101");
        answer.Code.Should().Be("1");
        answer.Name.Should().Be("Anulacion realizada");
        answer.Messages.Should().ContainSingle().Which.Should().Be(
            "Las secuencias fueron anuladas correctamente");
        answer.RawBody.Should().Contain("\"codigo\"");
    }

    [Fact]
    public async Task AnnulRangesAsync_WhenTheDgiiFails_ThrowsWithTheBodyItSent()
    {
        var handler = StubHttpMessageHandler.ReturningText("no disponible", HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        var act = () => client.AnnulRangesAsync(SignedAnecf(), "jwt");

        var exception = await act.Should().ThrowAsync<DgiiException>();
        exception.Which.RawBody.Should().Be("no disponible");
        exception.Which.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public void TheAnswerIsAnAnnulmentOnlyWhenTheDgiiSaysTheSequencesWereAnnulled()
    {
        var annulled = new DgiiRangeAnnulmentAnswer(
            "10100000101",
            "1",
            "Anulacion realizada",
            ["Las secuencias fueron anuladas correctamente"]);

        annulled.IsAnnulled.Should().BeTrue();
    }

    [Fact]
    public void TheAnswerThatTheDgiiRefusesIsNotAnAnnulment()
    {
        var refused = new DgiiRangeAnnulmentAnswer(
            "10100000101",
            "2",
            "Anulacion rechazada",
            ["Las secuencias que está intentando anular han sido utilizadas"]);

        refused.IsAnnulled.Should().BeFalse();
    }

    [Fact]
    public void TheAnswerThatCannotBeReadIsNotAnAnnulment()
    {
        var unknown = new DgiiRangeAnnulmentAnswer("10100000101", "9", null, []);

        unknown.IsAnnulled.Should().BeFalse(
            "an answer the code cannot read leaves the range as it was");
    }

    [Fact]
    public void TheAnnulmentWithExceptionsIsNotAnAnnulmentOfTheWholeRange()
    {
        var partial = new DgiiRangeAnnulmentAnswer(
            "10100000101",
            "1",
            "Anulacion realizada",
            ["Las secuencias fueron anuladas correctamente. Con excepción de las secuencias del detalle"]);

        partial.IsAnnulled.Should().BeFalse();
    }

    private static string Answer()
        => """
           {"rnc":"10100000101","codigo":"1","nombre":"Anulacion realizada","mensajes":["Las secuencias fueron anuladas correctamente"]}
           """;

    private static string SignedAnecf()
        => "<ANECF><Encabezado><RncEmisor>10100000101</RncEmisor></Encabezado></ANECF>";

    private static DgiiClient CreateClient(StubHttpMessageHandler handler)
        => new(
            new HttpClient(handler),
            new DgiiOptions { Environment = DgiiEnvironment.PreCertification });
}
