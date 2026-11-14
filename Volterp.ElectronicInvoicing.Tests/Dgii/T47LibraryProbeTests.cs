using System.Net;
using FluentAssertions;
using Volterp.ElectronicInvoicing.Dgii;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Dgii;

// Independent probes of T47 (read-only verifier): what the DGII says travels
// with the document, and what the DGII does not say never becomes a false.
public class T47LibraryProbeTests
{
    private const string WithoutSequence =
        """{"trackId":"t-1","codigo":3,"estado":"En proceso","mensajes":[]}""";

    private const string WithSequenceFalse =
        """{"trackId":"t-1","codigo":3,"estado":"En proceso","secuenciaUtilizada":false,"mensajes":[]}""";

    [Fact]
    public async Task AStatusWithoutSecuenciaUtilizada_IsNotAFalse()
    {
        var handler = StubHttpMessageHandler.ReturningJson(WithoutSequence);
        var client = CreateClient(handler);

        var status = await client.GetStatusAsync("t-1", "jwt");

        status.SequenceConsumed.Should().BeNull("the DGII did not say it");
        status.RawBody.Should().Be(WithoutSequence, "the body is what it said");
    }

    [Fact]
    public async Task AStatusThatSaysTheNumberIsUsable_IsFalse()
    {
        var handler = StubHttpMessageHandler.ReturningJson(WithSequenceFalse);
        var client = CreateClient(handler);

        var status = await client.GetStatusAsync("t-1", "jwt");

        status.SequenceConsumed.Should().BeFalse("the DGII said it");
    }

    [Fact]
    public async Task AFailedStatus_CarriesTheBodyOfTheDgii()
    {
        var handler = StubHttpMessageHandler.ReturningText("""{"error":"timeout"}""", HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        var act = () => client.GetStatusAsync("t-1", "jwt");

        var exception = await act.Should().ThrowAsync<DgiiException>();
        exception.Which.RawBody.Should().Be("""{"error":"timeout"}""");
    }

    [Fact]
    public async Task AFailedSend_CarriesTheBodyOfTheDgii()
    {
        var handler = StubHttpMessageHandler.ReturningText("""{"error":"rechazado"}""", HttpStatusCode.BadRequest);
        var client = CreateClient(handler);

        var act = () => client.SendElectronicInvoiceAsync(Document(), "jwt");

        var exception = await act.Should().ThrowAsync<DgiiException>();
        exception.Which.RawBody.Should().Be("""{"error":"rechazado"}""");
    }

    private static DgiiClient CreateClient(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new DgiiClient(httpClient, new DgiiOptions { Environment = DgiiEnvironment.PreCertification });
    }

    private static string Document()
        => "<e-CF><Encabezado><IdDoc><eNCF>E320000000001</eNCF></IdDoc>" +
           "<Emisor><RNCEmisor>101500018</RNCEmisor></Emisor></Encabezado></e-CF>";
}
