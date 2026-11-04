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
/// </remarks>
public sealed class DgiiAuthenticator(IDgiiClient client, IEcfSigner signer)
{
    private readonly IDgiiClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly IEcfSigner _signer = signer ?? throw new ArgumentNullException(nameof(signer));

    /// <summary>
    /// Authenticates the issuer and returns the token every later call carries.
    /// </summary>
    public async Task<DgiiToken> AuthenticateAsync(
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        var seed = await _client.GetSeedAsync(cancellationToken);
        var signedSeed = _signer.Sign(seed, certificate);

        return await _client.RequestTokenAsync(signedSeed, cancellationToken);
    }
}
