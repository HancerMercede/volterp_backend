using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Volterp.ElectronicInvoicing.Abstractions;
using Volterp.ElectronicInvoicing.Dgii;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Dgii;

public class DgiiAuthenticatorTests
{
    private const string SeedXml = "<SemillaModel><valor>abc</valor></SemillaModel>";

    [Fact]
    public async Task AuthenticateAsync_SignsTheSeedAndTradesItForAToken()
    {
        var client = new RecordingDgiiClient();
        var signer = new RecordingEcfSigner();
        var authenticator = new DgiiAuthenticator(client, signer);

        var token = await authenticator.AuthenticateAsync(new X509Certificate2());

        signer.SignedXml.Should().Be(SeedXml);
        client.TokenRequest.Should().Be("<signed-seed />");
        token.Token.Should().Be("jwt");
    }

    private sealed class RecordingDgiiClient : IDgiiClient
    {
        public int SeedRequests { get; private set; }
        public string? TokenRequest { get; private set; }

        public Task<string> GetSeedAsync(CancellationToken cancellationToken = default)
        {
            SeedRequests++;
            return Task.FromResult(SeedXml);
        }

        public Task<DgiiToken> RequestTokenAsync(string signedSeedXml, CancellationToken cancellationToken = default)
        {
            TokenRequest = signedSeedXml;
            return Task.FromResult(new DgiiToken("jwt", null, null));
        }

        public Task<DgiiReceipt> SendElectronicInvoiceAsync(string signedEcfXml, string token, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DgiiStatus> GetStatusAsync(string trackId, string token, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingEcfSigner : IEcfSigner
    {
        public string? SignedXml { get; private set; }

        public string Sign(string xml, X509Certificate2 certificate)
        {
            SignedXml = xml;
            return "<signed-seed />";
        }

        public bool Verify(string signedXml, X509Certificate2 certificate) => true;
    }
}
