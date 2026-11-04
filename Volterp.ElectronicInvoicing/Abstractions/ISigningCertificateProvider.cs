using System.Security.Cryptography.X509Certificates;

namespace Volterp.ElectronicInvoicing.Abstractions;

/// <summary>
/// Resolves the certificate an issuer signs its e-CF with. The certificate
/// belongs to the issuing RNC rather than to the tenant, because one tenant
/// may emit through several RNC (branches).
/// </summary>
public interface ISigningCertificateProvider
{
    Task<X509Certificate2> GetSigningCertificateAsync(string issuerRnc, CancellationToken cancellationToken = default);
}
