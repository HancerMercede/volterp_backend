using System.Net;
using System.Text;
using FluentAssertions;
using Volterp.ElectronicInvoicing.Dgii;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Dgii;

// Every URL asserted here comes from the "Descripción Técnica Servicios DGII"
// and not from the client that implements it: the page is cited on each one.
public class DgiiClientTests
{
    // "Descripción Técnica Servicios DGII", p. 10: the seed is
    // <SemillaModel><valor/><fecha/></SemillaModel>.
    private const string SeedXml = "<SemillaModel><valor>abc</valor><fecha>2019-03-13T14:33:32-04:00</fecha></SemillaModel>";

    [Theory]
    [InlineData(DgiiEnvironment.PreCertification, "testecf")]
    [InlineData(DgiiEnvironment.Certification, "certecf")]
    [InlineData(DgiiEnvironment.Production, "ecf")]
    public async Task GetSeedAsync_AsksTheEnvironmentTheClientIsConfiguredFor(
        DgiiEnvironment environment,
        string expectedSegment)
    {
        var handler = StubHttpMessageHandler.ReturningText(SeedXml);
        var client = CreateClient(handler, environment);

        await client.GetSeedAsync();

        // "Descripción Técnica Servicios DGII", p. 10:
        // GET {base}/{ambiente}/autenticacion/api/autenticacion/semilla
        handler.LastUri.Should().Be($"https://ecf.dgii.gov.do/{expectedSegment}/autenticacion/api/autenticacion/semilla");
    }

    [Fact]
    public async Task GetSeedAsync_ReturnsTheSeedVerbatim()
    {
        var handler = StubHttpMessageHandler.ReturningText(SeedXml);
        var client = CreateClient(handler);

        var seed = await client.GetSeedAsync();

        seed.Should().Be(SeedXml);
    }

    [Fact]
    public async Task GetSeedAsync_WhenTheDgiiFails_ThrowsWithTheBodyItSent()
    {
        var handler = StubHttpMessageHandler.ReturningText("no disponible", HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        var act = () => client.GetSeedAsync();

        var exception = await act.Should().ThrowAsync<DgiiException>();
        exception.Which.RawBody.Should().Be("no disponible");
        exception.Which.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task RequestTokenAsync_SendsTheSignedSeedAsTheXmlFieldOfAMultipartForm()
    {
        var handler = StubHttpMessageHandler.ReturningJson(
            """{"token":"jwt","expira":"2026-11-04T18:00:00Z","expedido":"2026-11-04T17:00:00Z"}""");
        var client = CreateClient(handler);

        await client.RequestTokenAsync(SeedXml);

        // "Descripción Técnica Servicios DGII", p. 11:
        // POST {base}/{ambiente}/autenticacion/api/autenticacion/validarsemilla,
        // with the signed seed as the xml field of a multipart form.
        handler.LastMethod.Should().Be("POST");
        handler.LastUri.Should().EndWith("/autenticacion/api/autenticacion/validarsemilla");
        handler.LastContentType.Should().StartWith("multipart/form-data");
        handler.LastBody.Should().MatchRegex("name=\"?xml\"?");
        handler.LastBody.Should().Contain("text/xml");
        handler.LastBody.Should().Contain("<SemillaModel>");
        handler.LastBody.Should().MatchRegex("filename=\"?response_",
            "the DGII expects the seed file named response_{timestamp}.xml.");
    }

    [Fact]
    public async Task RequestTokenAsync_ReturnsTheTokenWithItsExpiration()
    {
        var handler = StubHttpMessageHandler.ReturningJson(
            """{"token":"jwt","expira":"2026-11-04T18:00:00Z","expedido":"2026-11-04T17:00:00Z"}""");
        var client = CreateClient(handler);

        var token = await client.RequestTokenAsync(SeedXml);

        token.Token.Should().Be("jwt");
        token.ExpiresAt.Should().Be(new DateTimeOffset(2026, 11, 4, 18, 0, 0, TimeSpan.Zero));
        token.IssuedAt.Should().Be(new DateTimeOffset(2026, 11, 4, 17, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task RequestTokenAsync_WhenTheDgiiAnswersWithoutAToken_Throws()
    {
        var handler = StubHttpMessageHandler.ReturningJson("""{"expira":"2026-11-04T18:00:00Z"}""");
        var client = CreateClient(handler);

        var act = () => client.RequestTokenAsync(SeedXml);

        await act.Should().ThrowAsync<DgiiException>();
    }

    [Fact]
    public async Task RequestTokenAsync_WhenTheBodyIsNotJson_ThrowsWithTheBodyItSent()
    {
        var handler = StubHttpMessageHandler.ReturningText("<html>gateway error</html>");
        var client = CreateClient(handler);

        var act = () => client.RequestTokenAsync(SeedXml);

        var exception = await act.Should().ThrowAsync<DgiiException>();
        exception.Which.RawBody.Should().Contain("gateway error");
    }

    [Fact]
    public async Task SendElectronicInvoiceAsync_CarriesTheTokenAsABearerHeader()
    {
        var handler = StubHttpMessageHandler.ReturningJson("""{"trackId":"track-1"}""");
        var client = CreateClient(handler);

        await client.SendElectronicInvoiceAsync(Document(), "jwt");

        // "Descripción Técnica Servicios DGII", p. 13:
        // POST {base}/{ambiente}/recepcion/api/facturaselectronicas
        handler.LastUri.Should().EndWith("/recepcion/api/facturaselectronicas");
        handler.LastAuthorization.Should().Be("bearer jwt");
        handler.LastBody.Should().MatchRegex("name=\"?xml\"?");
        handler.LastBody.Should().Contain("<eNCF>E320000000001</eNCF>");
        handler.LastBody.Should().Contain("101500018E320000000001.xml",
            "the DGII expects the document file named RNC + e-NCF.");
    }

    [Fact]
    public async Task SendElectronicInvoiceAsync_WithoutTheIssuerRncAndTheEncf_Fails()
    {
        var handler = StubHttpMessageHandler.ReturningJson("""{"trackId":"track-1"}""");
        var client = CreateClient(handler);

        var act = () => client.SendElectronicInvoiceAsync("<e-CF />", "jwt");

        await act.Should().ThrowAsync<DgiiException>(
            "without the RNC and the e-NCF there is no name the DGII would accept.");
    }

    [Fact]
    public async Task SendElectronicInvoiceAsync_ReturnsTheTrackId()
    {
        var handler = StubHttpMessageHandler.ReturningJson("""{"trackId":"track-1"}""");
        var client = CreateClient(handler);

        var receipt = await client.SendElectronicInvoiceAsync(Document(), "jwt");

        receipt.TrackId.Should().Be("track-1");
        receipt.IsRejected.Should().BeFalse();
    }

    [Fact]
    public async Task SendElectronicInvoiceAsync_WhenTheDgiiReportsAnError_ReturnsItWithoutATrackId()
    {
        var handler = StubHttpMessageHandler.ReturningJson(
            """{"trackId":null,"error":"RNC no autorizado","mensaje":"rechazado"}""");
        var client = CreateClient(handler);

        var receipt = await client.SendElectronicInvoiceAsync(Document(), "jwt");

        receipt.IsRejected.Should().BeTrue();
        receipt.Error.Should().Be("RNC no autorizado");
        receipt.Message.Should().Be("rechazado");
        receipt.TrackId.Should().BeNull();
    }

    [Fact]
    public async Task SendElectronicInvoiceAsync_CarriesTheBodyOfTheAcknowledgement()
    {
        const string body = """{"trackId":"track-1","error":null,"mensaje":"Recibido"}""";
        var handler = StubHttpMessageHandler.ReturningJson(body);
        var client = CreateClient(handler);

        var receipt = await client.SendElectronicInvoiceAsync(Document(), "jwt");

        // What the DGII answered is kept with the document: the codes of its
        // messages only live in the body.
        receipt.RawBody.Should().Be(body);
    }

    [Fact]
    public async Task GetStatusAsync_AsksForTheVerdictOfTheTrackId()
    {
        var handler = StubHttpMessageHandler.ReturningJson(
            """{"trackId":"track-1","codigo":1,"estado":"Aceptado","rnc":"101","eNCF":"E310000000001","secuenciaUtilizada":true,"fechaRecepcion":"2026-11-04T17:00:00Z","mensajes":[]}""");
        var client = CreateClient(handler);

        await client.GetStatusAsync("track-1", "jwt");

        // "Descripción Técnica Servicios DGII", p. 22:
        // GET {base}/{ambiente}/consultaresultado/api/consultas/estado?trackid=
        handler.LastUri.Should().Contain("/consultaresultado/api/consultas/estado?trackid=track-1");
        handler.LastAuthorization.Should().Be("bearer jwt");
    }

    [Fact]
    public async Task GetStatusAsync_CarriesTheBodyOfTheAnswer()
    {
        const string body =
            """{"trackId":"track-1","codigo":2,"estado":"Rechazado","secuenciaUtilizada":true,"mensajes":[{"valor":"El RNC del receptor no existe","codigo":7}]}""";
        var handler = StubHttpMessageHandler.ReturningJson(body);
        var client = CreateClient(handler);

        var status = await client.GetStatusAsync("track-1", "jwt");

        // The answer of the DGII travels with the document: the code of each
        // of its messages only lives in the body.
        status.RawBody.Should().Be(body);
    }

    [Fact]
    public async Task GetStatusAsync_ReadsTheAnswerWhenTheDgiiCodesTheVerdictAsText()
    {
        // The DGII answers the code of the verdict as text in this service:
        // "codigo": "1" (Descripción Técnica Servicios DGII, p. 21, and the
        // answer of the test environment on 2026-11-19).
        const string body =
            """{"trackId":"track-1","codigo":"1","estado":"Aceptado","eNCF":"E310000000001","secuenciaUtilizada":true,"mensajes":[{"valor":"El comprobante fue recibido","codigo":"7"}]}""";
        var handler = StubHttpMessageHandler.ReturningJson(body);
        var client = CreateClient(handler);

        var status = await client.GetStatusAsync("track-1", "jwt");

        status.Verdict.Should().Be(DgiiVerdict.Accepted);
        status.Messages.Should().ContainSingle(message => message.Code == 7);
    }

    [Theory]
    [InlineData(0, DgiiVerdict.NotFound)]
    [InlineData(1, DgiiVerdict.Accepted)]
    [InlineData(2, DgiiVerdict.Rejected)]
    [InlineData(3, DgiiVerdict.InProcess)]
    [InlineData(4, DgiiVerdict.AcceptedConditionally)]
    public async Task GetStatusAsync_MapsTheDgiiVerdictCode(int code, DgiiVerdict expected)
    {
        var handler = StubHttpMessageHandler.ReturningJson(
            $$"""{"trackId":"track-1","codigo":{{code}},"estado":"x","secuenciaUtilizada":false,"mensajes":[]}""");
        var client = CreateClient(handler);

        var status = await client.GetStatusAsync("track-1", "jwt");

        status.Verdict.Should().Be(expected);
    }

    [Fact]
    public async Task GetStatusAsync_ReportsThatTheSequenceCannotBeReused()
    {
        var handler = StubHttpMessageHandler.ReturningJson(
            """{"trackId":"track-1","codigo":2,"estado":"Rechazado","secuenciaUtilizada":true,"mensajes":[]}""");
        var client = CreateClient(handler);

        var status = await client.GetStatusAsync("track-1", "jwt");

        status.SequenceConsumed.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatusAsync_ReadsTheReasonsBehindTheVerdict()
    {
        var handler = StubHttpMessageHandler.ReturningJson(
            """{"trackId":"track-1","codigo":2,"estado":"Rechazado","secuenciaUtilizada":true,"mensajes":[{"valor":"El RNC del receptor no existe","codigo":7}]}""");
        var client = CreateClient(handler);

        var status = await client.GetStatusAsync("track-1", "jwt");

        status.Messages.Should().HaveCount(1);
        status.Messages[0].Value.Should().Be("El RNC del receptor no existe");
        status.Messages[0].Code.Should().Be(7);
    }

    [Theory]
    [InlineData(DgiiVerdict.Accepted, EcfStatus.Accepted)]
    [InlineData(DgiiVerdict.AcceptedConditionally, EcfStatus.AcceptedConditionally)]
    [InlineData(DgiiVerdict.Rejected, EcfStatus.Rejected)]
    [InlineData(DgiiVerdict.InProcess, EcfStatus.InProcess)]
    public void ToEcfStatus_MapsTheVerdictToTheDocumentState(DgiiVerdict verdict, EcfStatus expected)
    {
        verdict.ToEcfStatus().Should().Be(expected);
    }

    [Fact]
    public void ToEcfStatus_HasNoDocumentStateForATrackIdTheDgiiDoesNotKnow()
    {
        DgiiVerdict.NotFound.ToEcfStatus().Should().BeNull();
    }

    [Fact]
    public void ATokenPastItsExpirationIsExpired()
    {
        var expired = new DgiiToken("jwt", DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(-1));
        var alive = new DgiiToken("jwt", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow);

        expired.IsExpired.Should().BeTrue();
        alive.IsExpired.Should().BeFalse();
    }

    private static string Document()
        => "<e-CF><Encabezado><IdDoc><eNCF>E320000000001</eNCF></IdDoc>" +
           "<Emisor><RNCEmisor>101500018</RNCEmisor></Emisor></Encabezado></e-CF>";

    // "Descripción Técnica Servicios DGII", p. 30: the track id of a document
    // is asked by the issuer RNC and the e-NCF. It is how a lost answer is
    // recovered without sending the document twice.
    [Fact]
    public async Task GetTrackIdAsync_AsksForTheTrackIdOfTheEncf()
    {
        var handler = StubHttpMessageHandler.ReturningJson(
            """{"trackId":"track-9","estado":"Aceptado","fechaRecepcion":"2026-11-13T10:00:00"}""");
        var client = CreateClient(handler);

        await client.GetTrackIdAsync("10100000101", "E320000000001", "jwt");

        // "Descripción Técnica Servicios DGII", p. 30:
        // GET {base}/{ambiente}/consultatrackids/api/trackids/consulta?rncemisor=&encf=
        handler.LastUri.Should().Be(
            "https://ecf.dgii.gov.do/testecf/consultatrackids/api/trackids/consulta?rncemisor=10100000101&encf=E320000000001");
        handler.LastMethod.Should().Be("GET");
        handler.LastAuthorization.Should().Be("bearer jwt");
    }

    [Fact]
    public async Task GetTrackIdAsync_ReturnsTheTrackIdWithTheStateAndTheDayItArrived()
    {
        var handler = StubHttpMessageHandler.ReturningJson(
            """{"trackId":"track-9","estado":"Aceptado","fechaRecepcion":"2026-11-13T10:00:00"}""");
        var client = CreateClient(handler);

        var lookup = await client.GetTrackIdAsync("10100000101", "E320000000001", "jwt");

        lookup.TrackId.Should().Be("track-9");
        lookup.Estado.Should().Be("Aceptado");
        lookup.NotFound.Should().BeFalse();
        lookup.ReceivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTrackIdAsync_WhenTheDgiiDoesNotHaveTheDocument_SaysItWasNotFound()
    {
        var handler = StubHttpMessageHandler.ReturningJson(
            """{"trackId":"","estado":"No encontrado","fechaRecepcion":""}""");
        var client = CreateClient(handler);

        var lookup = await client.GetTrackIdAsync("10100000101", "E320000000001", "jwt");

        lookup.NotFound.Should().BeTrue("a document the DGII never received can be sent again");
    }

    [Fact]
    public async Task GetTrackIdAsync_WhenTheDgiiFails_ThrowsWithTheBodyItSent()
    {
        var handler = StubHttpMessageHandler.ReturningText("no disponible", HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        var act = () => client.GetTrackIdAsync("10100000101", "E320000000001", "jwt");

        var exception = await act.Should().ThrowAsync<DgiiException>();
        exception.Which.RawBody.Should().Be("no disponible");
    }

    private static DgiiClient CreateClient(
        StubHttpMessageHandler handler,
        DgiiEnvironment environment = DgiiEnvironment.PreCertification)
    {
        var httpClient = new HttpClient(handler);
        return new DgiiClient(httpClient, new DgiiOptions { Environment = environment });
    }
}
