using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;

namespace Volterp.Api.Authentication;

public sealed class DgiiCertificateAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SectionName = "DgiiCertificate";
    public const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

    public X509RevocationMode RevocationMode { get; set; } = X509RevocationMode.Online;

    public X509ChainTrustMode ChainTrustValidationMode { get; set; }
        = X509ChainTrustMode.CustomRootTrust;

    public List<string> TrustedRootCertificatePaths { get; } = [];

    public X509Certificate2Collection TrustedRootCertificates { get; } = [];

    public void LoadTrustedRootCertificates()
    {
        foreach (var path in TrustedRootCertificatePaths)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("A trusted certificate path cannot be empty.");

            if (!File.Exists(path))
                throw new FileNotFoundException("The trusted certificate file does not exist.", path);

            var certificate = X509CertificateLoader.LoadCertificateFromFile(path);
            if (!TrustedRootCertificates.Any(existing =>
                    string.Equals(existing.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase)))
            {
                TrustedRootCertificates.Add(certificate);
            }
        }
    }
}
