using System.Security.Cryptography.X509Certificates;
using Volterp.ElectronicInvoicing.Abstractions;

namespace Volterp.ElectronicInvoicing.Dgii;

// Seed, signed with the issuer's certificate, traded for a token. There is no
// user and no password in this protocol: the certificate is the whole
// credential, and the same signer signs the seed and the e-CF.
//
// The token belongs to the cache, which is what outlives a request.
public sealed class DgiiAuthenticator(IDgiiClient client, IEcfSigner signer, DgiiTokenCache cache)
{
    private readonly IDgiiClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly IEcfSigner _signer = signer ?? throw new ArgumentNullException(nameof(signer));
    private readonly DgiiTokenCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    public Task<DgiiToken> AuthenticateAsync(
        string issuerRnc,
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerRnc);
        ArgumentNullException.ThrowIfNull(certificate);

        return _cache.GetOrRenewAsync(
            issuerRnc,
            ct => RequestTokenAsync(certificate, ct),
            cancellationToken);
    }

    private async Task<DgiiToken> RequestTokenAsync(
        X509Certificate2 certificate,
        CancellationToken cancellationToken)
    {
        var seed = await _client.GetSeedAsync(cancellationToken);
        var signedSeed = _signer.Sign(seed, certificate);

        // The DGII's error travels untouched, and the dead token is never
        // served in its place.
        return await _client.RequestTokenAsync(signedSeed, cancellationToken);
    }
}
