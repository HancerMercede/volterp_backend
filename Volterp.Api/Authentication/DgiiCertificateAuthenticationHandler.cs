using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Volterp.Api.Authentication;

public sealed class DgiiCertificateAuthenticationHandler(
    IOptionsMonitor<DgiiCertificateAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<DgiiCertificateAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var certificate = Context.Connection.ClientCertificate;
        if (certificate is null)
            return Task.FromResult(AuthenticateResult.Fail("A client certificate is required."));

        var failure = Validate(certificate);
        if (failure is not null)
            return Task.FromResult(AuthenticateResult.Fail(failure));

        var identity = new ClaimsIdentity(
            [
                new Claim("dgiiCertificateThumbprint", certificate.Thumbprint),
                new Claim(ClaimTypes.Name, certificate.Subject)
            ],
            Scheme.Name);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private string? Validate(X509Certificate2 certificate)
    {
        var now = TimeProvider.GetUtcNow();

        if (certificate.NotBefore.ToUniversalTime() > now.UtcDateTime
            || certificate.NotAfter.ToUniversalTime() < now.UtcDateTime)
        {
            return "The client certificate validity period does not cover the current time.";
        }

        if (!HasClientAuthenticationUsage(certificate))
            return "The client certificate does not allow client authentication.";

        if (!HasUsableKeyUsage(certificate))
            return "The client certificate does not allow digital signature or key encipherment.";

        var basicConstraints = certificate.Extensions
            .OfType<X509BasicConstraintsExtension>()
            .FirstOrDefault();
        if (basicConstraints?.CertificateAuthority == true)
            return "The client certificate cannot be a certificate authority.";

        if (!HasStrongPublicKey(certificate))
            return "The client certificate public key is too weak.";

        try
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            chain.ChainPolicy.RevocationMode = Options.RevocationMode;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.VerificationTime = now.UtcDateTime;
            chain.ChainPolicy.DisableCertificateDownloads = true;
            chain.ChainPolicy.ApplicationPolicy.Add(
                new Oid(DgiiCertificateAuthenticationOptions.ClientAuthenticationOid));

            if (Options.ChainTrustValidationMode == X509ChainTrustMode.CustomRootTrust)
            {
                if (Options.TrustedRootCertificates.Count == 0)
                    return "The client certificate is not trusted because no trust anchor is configured.";

                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                foreach (var root in Options.TrustedRootCertificates)
                    chain.ChainPolicy.CustomTrustStore.Add(root);
            }
            else
            {
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.System;
            }

            if (!chain.Build(certificate))
                return "The client certificate is not trusted.";

            if (chain.ChainElements.Count == 0
                || !string.Equals(
                    chain.ChainElements[0].Certificate.Thumbprint,
                    certificate.Thumbprint,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "The client certificate is not trusted.";
            }

            return null;
        }
        catch (CryptographicException)
        {
            return "The client certificate could not be validated.";
        }
    }

    private static bool HasClientAuthenticationUsage(X509Certificate2 certificate)
    {
        var extension = certificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .FirstOrDefault();

        return extension is not null
            && extension.EnhancedKeyUsages.Cast<Oid>().Any(oid =>
                string.Equals(
                    oid.Value,
                    DgiiCertificateAuthenticationOptions.ClientAuthenticationOid,
                    StringComparison.Ordinal));
    }

    private static bool HasUsableKeyUsage(X509Certificate2 certificate)
    {
        var extension = certificate.Extensions
            .OfType<X509KeyUsageExtension>()
            .FirstOrDefault();

        if (extension is null)
            return true;

        return (extension.KeyUsages
            & (X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment)) != 0;
    }

    private static bool HasStrongPublicKey(X509Certificate2 certificate)
    {
        using var rsa = certificate.GetRSAPublicKey();
        if (rsa is not null)
            return rsa.KeySize >= 2048;

        using var ecdsa = certificate.GetECDsaPublicKey();
        return ecdsa is not null && ecdsa.KeySize >= 256;
    }
}
