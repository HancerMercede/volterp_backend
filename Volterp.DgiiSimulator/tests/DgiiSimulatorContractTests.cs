using System.Net;
using System.Text.Json;
using Xunit;

namespace Volterp.DgiiSimulator.Tests;

/// <summary>
/// The contract of the DGII that the simulator has to honour exactly: the
/// shape of the paths, the security code and what the flag of the sequence
/// means.
/// </summary>
public sealed class DgiiSimulatorContractTests
{
    [Fact]
    public async Task TheSecurityCode_IsTheFirstSixCharactersOfTheSignatureValue()
    {
        await using var simulator = await SimulatorFlow.StartAsync();
        using var client = new HttpClient { BaseAddress = simulator.BaseAddress };

        var token = await SimulatorFlow.GetTokenAsync(client);
        var signed = SignedEcfWithNonAlphanumericSignatureValue();
        var signatureValue = SimulatorFlow.ExtractSignatureValue(signed)!;

        Assert.Contains(signatureValue[..6], character => character is '+' or '/');

        await SimulatorFlow.ReceiveAsync(client, token, signed);

        using var response = await SimulatorFlow.GetAsync(
            client,
            token,
            $"consultaestado/api/consultas/estado?rncemisor={SimulatorFlow.IssuerRnc}&ncfelectronico={SimulatorFlow.Encf}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(signatureValue[..6], json.RootElement.GetProperty("codigoSeguridad").GetString());
    }

    [Fact]
    public async Task TheEnvironmentSegment_IsCaseSensitive()
    {
        await using var simulator = await SimulatorFlow.StartAsync();
        using var client = new HttpClient { BaseAddress = simulator.BaseAddress };

        using var response = await client.GetAsync(
            "/TESTECF/autenticacion/api/autenticacion/semilla");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheServicePath_IsCaseSensitive()
    {
        await using var simulator = await SimulatorFlow.StartAsync();
        using var client = new HttpClient { BaseAddress = simulator.BaseAddress };

        using var response = await client.GetAsync(
            $"/{SimulatorFlow.Environment}/autenticacion/api/autenticacion/Semilla");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Status_OfAnAcceptedDocument_ReportsTheSequenceAsConsumed()
    {
        await using var simulator = await SimulatorFlow.StartAsync();
        using var client = new HttpClient { BaseAddress = simulator.BaseAddress };

        var token = await SimulatorFlow.GetTokenAsync(client);
        var trackId = await SimulatorFlow.ReceiveAsync(client, token);

        using var response = await SimulatorFlow.GetAsync(
            client,
            token,
            $"consultaresultado/api/consultas/estado?trackid={trackId}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(1, json.RootElement.GetProperty("codigo").GetInt32());
        Assert.True(json.RootElement.GetProperty("secuenciaUtilizada").GetBoolean());
    }

    [Fact]
    public async Task Status_OfARejectedDocument_ReportsTheSequenceAsReusable()
    {
        await using var simulator = await SimulatorFlow.StartAsync();
        using var client = new HttpClient { BaseAddress = simulator.BaseAddress };

        var token = await SimulatorFlow.GetTokenAsync(client);
        var trackId = await SimulatorFlow.ReceiveAsync(client, token, verdict: "rejected");

        using var response = await SimulatorFlow.GetAsync(
            client,
            token,
            $"consultaresultado/api/consultas/estado?trackid={trackId}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(2, json.RootElement.GetProperty("codigo").GetInt32());
        Assert.False(json.RootElement.GetProperty("secuenciaUtilizada").GetBoolean());
    }

    /// <summary>
    /// A signature whose first six characters carry a character that is not a
    /// letter or a digit, which is where the security code of the contract
    /// stops being the first six characters of the signature.
    /// </summary>
    private static string SignedEcfWithNonAlphanumericSignatureValue()
    {
        for (var attempt = 0; attempt < 500; attempt++)
        {
            var signed = SimulatorFlow.SignedEcfXml($"<Observacion>{attempt}</Observacion>");
            var signatureValue = SimulatorFlow.ExtractSignatureValue(signed);

            if (signatureValue is { Length: > 6 } && signatureValue[..6].Any(character => character is '+' or '/'))
                return signed;
        }

        throw new InvalidOperationException("No signature with a non-alphanumeric character was found.");
    }
}
