using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Signing;
using Xunit;
using Xunit.Abstractions;

namespace Volterp.ElectronicInvoicing.Tests.Dgii;

// The circuit against the DGII: the same client, the same signer and the
// same steps that the simulator exercises, with the base URL coming from the
// environment.
//
// Against the real DGII the issuer's own certificate is needed, so it comes
// from the environment too. Against the simulator it is not: the simulator
// does not check the signature, it only checks that the document arrives
// signed, so a certificate made here is enough to exercise the transport.
public class AgainstTheDgii
{
    private readonly ITestOutputHelper _output;

    public AgainstTheDgii(ITestOutputHelper output) => _output = output;

    [DgiiEnvironmentFact]
    public async Task TheDgiiGivesUsATokenForTheSeedSignedWithTheIssuerCertificate()
    {
        var client = CreateClient();
        using var certificate = LoadCertificate();

        var seed = await client.GetSeedAsync();
        _output.WriteLine($"Semilla: {seed}");

        var signedSeed = new XmlDsigEcfSigner().Sign(seed, certificate);

        DgiiToken token;
        try
        {
            token = await client.RequestTokenAsync(signedSeed);
        }
        catch (DgiiException exception)
        {
            // The body of the answer is what says which link of the chain is
            // missing, so it is part of the failure.
            throw new InvalidOperationException(
                $"The DGII refused the signed seed: {exception.Message} | {exception.RawBody}", exception);
        }

        token.Token.Should().NotBeNullOrWhiteSpace();
        token.ExpiresAt.Should().NotBeNull("the token carries the hour it lives");
        _output.WriteLine($"Token hasta: {token.ExpiresAt:o}");
    }

    [DgiiEnvironmentFact]
    public async Task TheDocumentTravelsAndTheVerdictArrives()
    {
        var client = CreateClient();
        using var certificate = LoadCertificate();

        var seed = await client.GetSeedAsync();
        var token = await client.RequestTokenAsync(new XmlDsigEcfSigner().Sign(seed, certificate));

        var signed = new XmlDsigEcfSigner().Sign(EcfXml(), certificate);

        var receipt = await client.SendElectronicInvoiceAsync(signed, token.Token);

        receipt.TrackId.Should().NotBeNullOrWhiteSpace("the DGII answers with the track id of what it received");

        // The verdict is not immediate: the DGII validates first, so the
        // answer is asked until it is a final one.
        DgiiStatus status = await client.GetStatusAsync(receipt.TrackId!, token.Token);
        var attempts = 1;

        while (status.Verdict == DgiiVerdict.InProcess && attempts < 20)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            status = await client.GetStatusAsync(receipt.TrackId!, token.Token);
            attempts++;
        }

        _output.WriteLine($"Track id {receipt.TrackId} → {status.Verdict} ({status.StatusText})");
        status.Verdict.Should().NotBe(DgiiVerdict.InProcess, "the verdict arrives sooner or later");
    }

    // The name of the file the DGII expects is taken from the document, so it
    // has to say who emits it and under which number. Against the real DGII
    // those are the taxpayer's: they come from the environment and never from
    // the repository.
    private const string EcfTemplate = """
        <?xml version="1.0" encoding="utf-8"?>
        <ECF Version="1.0">
          <Encabezado>
            <Emisor>
              <RNCEmisor>{{RNCEMISOR}}</RNCEmisor>
            </Emisor>
            <TipoIngresos>01</TipoIngresos>
            <eNCF>{{ENCF}}</eNCF>
            <FechaEmision>{{FECHAEMISION}}</FechaEmision>
          </Encabezado>
        </ECF>
        """;

    private static string EcfXml()
    {
        var issuer = DgiiEnvironmentSettings.IssuerRnc;
        var encf = DgiiEnvironmentSettings.Encf;

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(encf))
            throw new InvalidOperationException(
                "The issuer and the number come from the environment "
                + "(VOLTERP_DGII_ISSUER_RNC, VOLTERP_DGII_ENCF): against the real DGII the document "
                + "has to be the taxpayer's own.");

        return EcfTemplate
            .Replace("{{RNCEMISOR}}", issuer)
            .Replace("{{ENCF}}", encf)
            .Replace("{{FECHAEMISION}}", DateTime.UtcNow.ToString("dd-MM-yyyy"));
    }

    private static DgiiClient CreateClient()
        => new(
            new HttpClient(),
            new DgiiOptions
            {
                BaseUrl = DgiiEnvironmentSettings.BaseUrl!,
                Environment = DgiiEnvironmentSettings.Environment
            });

    private static X509Certificate2 LoadCertificate()
    {
        var path = DgiiEnvironmentSettings.CertificatePath;

        if (!string.IsNullOrWhiteSpace(path))
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"The issuer certificate {path} is not there.", path);

            return X509CertificateLoader.LoadPkcs12FromFile(path, DgiiEnvironmentSettings.CertificatePassword);
        }

        // Against the real DGII a certificate made here proves nothing: the
        // DGII only trusts the certificate the taxpayer registered.
        if (DgiiEnvironmentRequirements.IsTheRealDgii(DgiiEnvironmentSettings.BaseUrl))
            throw new InvalidOperationException(
                "The issuer certificate comes from the environment (VOLTERP_DGII_CERTIFICATE): "
                + "against the real DGII a certificate made here is not accepted.");

        return CreateSelfSignedCertificate();
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Volterp Test Emisor, C=DO",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));

        return X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pfx, "volterp-test-password"),
            "volterp-test-password",
            X509KeyStorageFlags.EphemeralKeySet);
    }
}
