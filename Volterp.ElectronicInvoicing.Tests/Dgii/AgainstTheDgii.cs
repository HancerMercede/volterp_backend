using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Signing;
using Xunit;
using Xunit.Abstractions;

namespace Volterp.ElectronicInvoicing.Tests.Dgii;

// The circuit against the DGII itself: the same client, the same signer and
// the same steps that the simulator exercises, with the base URL and the
// certificate coming from the environment.
//
// It is the test that says whether the issuer is really enabled: the answer
// of the DGII tells which link of the chain is missing.
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
        _output.WriteLine($"TokenHasta: {token.ExpiresAt:o}");
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
        var path = DgiiEnvironmentSettings.CertificatePath!;

        if (!File.Exists(path))
            throw new FileNotFoundException($"The issuer certificate {path} is not there.", path);

        return X509CertificateLoader.LoadPkcs12FromFile(path, DgiiEnvironmentSettings.CertificatePassword);
    }
}
