using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Volterp.ElectronicInvoicing.Tests;

/// <summary>
/// The certificates the tests sign with.
/// </summary>
public static class TestCertificate
{
    private const string Password = "volterp-test-password";

    /// <summary>
    /// A self-signed certificate that stands for the issuer of an e-CF.
    /// </summary>
    public static X509Certificate2 Create(string commonName = "Volterp Test Emisor")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={commonName}, C=DO",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));

        return X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pfx, Password),
            Password,
            X509KeyStorageFlags.EphemeralKeySet);
    }
}
