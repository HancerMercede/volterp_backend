using System.Security.Cryptography.X509Certificates;
using Volterp.ElectronicInvoicing.Abstractions;

namespace Volterp.ElectronicInvoicing.Dgii;

/// <summary>
/// The authentication round trip the DGII requires: ask for a seed, sign it
/// with the issuer's certificate and trade it for a token.
/// </summary>
/// <remarks>
/// The seed is XML and the signature profile is the same one the e-CF uses, so
/// the same signer does both jobs. There is no username and no password in this
/// protocol: the certificate is the whole credential.
/// <para>
/// The token belongs to the issuer whose certificate signed the seed and the
/// DGII says how long it lives, so the round trip is paid once per issuer and
/// not once per emission: the cache holds the token until it expires.
/// </para>
/// <para>
/// This class is the behaviour of the round trip and nothing else; the token
/// it produces belongs to the cache, which is the part that outlives a
/// request.
/// </para>
/// </remarks>
public sealed class DgiiAuthenticator(IDgiiClient client, IEcfSigner signer, DgiiTokenCache cache)
{
    private readonly IDgiiClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly IEcfSigner _signer = signer ?? throw new ArgumentNullException(nameof(signer));
    private readonly DgiiTokenCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    /// <summary>
    /// Authenticates the issuer and returns the token every later call carries.
    /// </summary>
    /// <param name="issuerRnc">
    /// The RNC of the issuer the token belongs to. The DGII issues the token
    /// to the issuer that signed the seed, so two issuers never share one.
    /// </param>
    /// <param name="certificate">The certificate of that issuer.</param>
    /// <param name="cancellationToken">Cancels the round trip.</param>
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

        // Whatever the DGII says about the renewal travels untouched: it is
        // the DGII that explains itself, and a token that could not be
        // renewed is never replaced by the one that already died.
        return await _client.RequestTokenAsync(signedSeed, cancellationToken);
    }
}
