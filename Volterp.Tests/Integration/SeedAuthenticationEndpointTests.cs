using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Api.Authentication;
using Volterp.Api.Controllers;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.Infrastructure.Data;
using Volterp.Tests.Authentication;
using Volterp.Tests.Services;
using Xunit;

namespace Volterp.Tests.Integration;

// The seed and the validation of the certificate: the two routes of the
// family "Comunicación Emisor-Receptor" that this platform publishes.
[Collection("VolterpApiFactory")]
public class SeedAuthenticationEndpointTests : IClassFixture<ApiFactory>
{
    private const string IssuerRnc = "130862346";
    private const string ReceiverRnc = "130862354";
    private const string Encf = "E310000000001";
    private const string SignerId = "00123456789";

    private readonly ApiFactory _factory;

    public SeedAuthenticationEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureSchemaCreated();
    }

    [Fact]
    public async Task SeedEndpoint_AnswersTheSemillaModelXml()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/fe/autenticacion/api/semilla");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/xml");

        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        document.Root!.Name.LocalName.Should().Be("SemillaModel");
        document.Root.Element("valor")!.Value.Should().NotBeNullOrWhiteSpace();
        document.Root.Element("fecha")!.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SeedEndpoint_KeepsTheSeedItIssued()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/fe/autenticacion/api/semilla");
        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        var value = document.Root!.Element("valor")!.Value;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        var stored = await db.Set<IssuedSeed>().AsNoTracking().SingleAsync(seed => seed.Value == value);
        stored.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        stored.UsedAt.Should().BeNull();
    }

    [Fact]
    public async Task ValidationEndpoint_WithTheSeedSignedByTheCaller_AnswersATokenWithItsExpiry()
    {
        await using var factory = await AuthenticatedFactoryAsync();
        var client = factory.CreateClient();

        var seed = await SeedAsync(client);
        var response = await client.PostAsync(
            "/fe/autenticacion/api/validacioncertificado",
            SignedContent(Sign(SeedXml(seed.Value))));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        root.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();

        var expiresAt = root.GetProperty("expira").GetString();
        expiresAt.Should().NotBeNullOrWhiteSpace();
        DateTimeOffset
            .Parse(expiresAt!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal)
            .Should().BeAfter(DateTimeOffset.UtcNow);

        var issuedAt = root.GetProperty("expedido").GetString();
        issuedAt.Should().NotBeNullOrWhiteSpace();
        DateTimeOffset
            .Parse(issuedAt!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal)
            .Should().BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ValidationEndpoint_WithASeedThatIsNotOurs_FailsClosed()
    {
        await using var factory = await AuthenticatedFactoryAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/fe/autenticacion/api/validacioncertificado",
            SignedContent(Sign(SeedXml(Guid.NewGuid().ToString()))));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("error").GetString().Should().Contain("not issued");
    }

    [Fact]
    public async Task ValidationEndpoint_WithAnExpiredSeed_FailsClosed()
    {
        await using var factory = await AuthenticatedFactoryAsync();
        var client = factory.CreateClient();

        var value = Guid.NewGuid().ToString();
        await StoreSeedAsync(factory, value, DateTime.UtcNow.AddMinutes(-1));

        var response = await client.PostAsync(
            "/fe/autenticacion/api/validacioncertificado",
            SignedContent(Sign(SeedXml(value))));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("error").GetString().Should().Contain("expired");
    }

    [Fact]
    public async Task ValidationEndpoint_WithASeedUsedTwice_FailsClosed()
    {
        await using var factory = await AuthenticatedFactoryAsync();
        var client = factory.CreateClient();

        var seed = await SeedAsync(client);
        var signed = Sign(SeedXml(seed.Value));

        var first = await client.PostAsync("/fe/autenticacion/api/validacioncertificado", SignedContent(signed));
        var second = await client.PostAsync("/fe/autenticacion/api/validacioncertificado", SignedContent(signed));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("error").GetString().Should().Contain("already used");
    }

    [Fact]
    public async Task ValidationEndpoint_WithADocumentWhoseSignatureDoesNotVerify_FailsClosed()
    {
        await using var factory = await AuthenticatedFactoryAsync();
        var client = factory.CreateClient();

        var seed = await SeedAsync(client);
        var signed = Sign(SeedXml(seed.Value));

        // The value of the seed is inside what the signature covers, so a
        // document that carries another one cannot be the one that was signed.
        var tampered = signed.Replace(seed.Value, Guid.NewGuid().ToString(), StringComparison.Ordinal);

        var response = await client.PostAsync(
            "/fe/autenticacion/api/validacioncertificado",
            SignedContent(tampered));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("error").GetString().Should().Contain("signature");
    }

    [Fact]
    public async Task TheTokenOfTheThirdParty_DoesNotOpenTheReceptionWithoutTheClientCertificate()
    {
        await using var factory = await AuthenticatedFactoryAsync();
        var client = factory.CreateClient();

        var seed = await SeedAsync(client);
        var validation = await client.PostAsync(
            "/fe/autenticacion/api/validacioncertificado",
            SignedContent(Sign(SeedXml(seed.Value))));

        validation.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await validation.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("token").GetString();

        // The token proves that the seed was ours. It is not a credential of
        // ours: the reception keeps demanding the certificate of the client.
        await using var withoutCertificate = new ApiFactory();
        withoutCertificate.EnsureSchemaCreated();
        var anonymous = withoutCertificate.CreateClient();
        anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await anonymous.PostAsync(
            "/fe/recepcion/api/ecf",
            ReceptionContent(
                ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
                ReceiverRnc,
                Encf));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<ApiFactory> AuthenticatedFactoryAsync()
    {
        var authority = TestCertificateAuthority.Create();
        var clientCertificate = authority.IssueClientCertificate();
        var factory = new ApiFactory(services =>
        {
            services.Configure<DgiiCertificateAuthenticationOptions>(
                ElectronicInvoiceReceptionController.DgiiAuthenticationScheme,
                options =>
                {
                    options.RevocationMode = X509RevocationMode.NoCheck;
                    options.TrustedRootCertificates.Add(authority.Root);
                });
            services.AddSingleton<IStartupFilter>(
                new ClientCertificateStartupFilter(authority.Root, clientCertificate));
        });

        factory.EnsureSchemaCreated();
        return factory;
    }

    private static async Task<SeedValue> SeedAsync(HttpClient client)
    {
        var response = await client.GetAsync("/fe/autenticacion/api/semilla");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());

        return new SeedValue(
            document.Root!.Element("valor")!.Value,
            document.Root.Element("fecha")!.Value);
    }

    private static async Task StoreSeedAsync(ApiFactory factory, string value, DateTime expiresAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        db.Set<IssuedSeed>().Add(new IssuedSeed
        {
            Id = Guid.CreateVersion7(),
            Value = value,
            IssuedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = expiresAt
        });

        await db.SaveChangesAsync();
    }

    // The shape of the seed is the one the DGII publishes (p. 44 of the
    // "Descripción Técnica Servicios DGII").
    private static string SeedXml(string value) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
        + "<SemillaModel xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\""
        + " xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">"
        + $"<valor>{value}</valor>"
        + "<fecha>2026-11-17T15:00:00.0000000+00:00</fecha>"
        + "</SemillaModel>";

    private static string Sign(string xml)
    {
        using var certificate = SignerCertificate();
        return new XmlDsigEcfSigner().Sign(xml, certificate);
    }

    // The certificate of the person that signs for the taxpayer carries the
    // id of the owner in the SN of its subject (p. 13 of the "Descripción
    // Técnica Emisores Electrónicos").
    private static X509Certificate2 SignerCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName($"CN=Volterp Third Party, SN={SignerId}"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));

        return X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pfx, "seed-test-password"),
            "seed-test-password",
            X509KeyStorageFlags.EphemeralKeySet);
    }

    private static MultipartFormDataContent SignedContent(string xml)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(xml));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/xml");

        return new MultipartFormDataContent
        {
            { file, "xml", $"response_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.xml" }
        };
    }

    private static MultipartFormDataContent ReceptionContent(
        string xml,
        string receiverRnc,
        string encf)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(xml));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/xml");

        return new MultipartFormDataContent
        {
            { file, "xml", ReceptionTestDocuments.FileName(receiverRnc, encf) }
        };
    }

    private sealed record SeedValue(string Value, string Fecha);

    private sealed class ClientCertificateStartupFilter(
        X509Certificate2 root,
        X509Certificate2 clientCertificate) : IStartupFilter, IDisposable
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            => app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    context.Connection.ClientCertificate = clientCertificate;
                    await nextMiddleware();
                });
                next(app);
            };

        public void Dispose()
        {
            root.Dispose();
            clientCertificate.Dispose();
        }
    }
}
