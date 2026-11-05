using Volterp.Application.Credentials;
using Volterp.Application.Interfaces;
using Volterp.Domain.ValueObjects;
using Volterp.ElectronicInvoicing.Abstractions;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Volterp.Infrastructure.Services;

/// <summary>
/// Resolves the certificate an issuer signs its e-CF with. The certificate
/// lives in the company certificate row, encrypted at rest, and it is indexed
/// by the issuer RNC because a tenant may emit through several RNC.
/// </summary>
public sealed class CompanySigningCertificateProvider(
    IUnitOfWork unitOfWork,
    ICredentialProtector credentialProtector) : ISigningCertificateProvider
{
    public async Task<X509Certificate2> GetSigningCertificateAsync(string issuerRnc, CancellationToken cancellationToken = default)
    {
        if (!Rnc.TryParse(issuerRnc, out var rnc))
            throw new InvalidOperationException($"'{issuerRnc}' is not a valid RNC.");

        var certificate = await unitOfWork.CompanyCertificates
            .GetActiveByTaxIdAsync(rnc.Value, cancellationToken);

        if (certificate is null)
            throw new InvalidOperationException($"There is no active signing certificate for the issuer RNC {rnc.Value}.");

        // The key is derived from the company the certificate belongs to, so a
        // credential encrypted for one tenant can never be opened with another.
        string credential;
        try
        {
            credential = credentialProtector.Unprotect(certificate.CompanyId, certificate.EncryptedCredential);
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException(
                $"The credential of the signing certificate of the issuer RNC {rnc.Value} could not be decrypted.");
        }

        var material = SigningCredential.Parse(credential);

        try
        {
            var signingCertificate = X509CertificateLoader.LoadPkcs12(
                Convert.FromBase64String(material.PfxBase64),
                material.Password,
                X509KeyStorageFlags.EphemeralKeySet);

            // The row carries the metadata the user declared; the certificate
            // carries its own validity. They can disagree, and only the
            // certificate decides whether this key may sign today.
            if (!signingCertificate.HasPrivateKey)
                throw new InvalidOperationException(
                    $"The signing certificate of the issuer RNC {rnc.Value} has no private key: it cannot sign an e-CF.");

            var now = DateTime.UtcNow;
            if (signingCertificate.NotBefore.ToUniversalTime() > now || signingCertificate.NotAfter.ToUniversalTime() <= now)
                throw new InvalidOperationException(
                    $"The signing certificate of the issuer RNC {rnc.Value} is not valid today (it is valid from {signingCertificate.NotBefore:dd-MM-yyyy} to {signingCertificate.NotAfter:dd-MM-yyyy}).");

            return signingCertificate;
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException(
                $"The signing certificate of the issuer RNC {rnc.Value} could not be opened: the pfx or its password is wrong.");
        }
    }
}
