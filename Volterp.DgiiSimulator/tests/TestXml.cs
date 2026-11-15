using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace Volterp.DgiiSimulator.Tests;

internal static class TestXml
{
    public static X509Certificate2 CreateCertificate(string commonName = "Volterp Test Emisor")
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
            certificate.Export(X509ContentType.Pfx, "volterp-test-password"),
            "volterp-test-password",
            X509KeyStorageFlags.EphemeralKeySet);
    }

    public static string Sign(string xml, X509Certificate2 certificate)
    {
        var document = new XmlDocument { PreserveWhitespace = false };
        document.LoadXml(xml);

        var signedXml = new SignedXml(document) { SigningKey = certificate.GetRSAPrivateKey() };
        signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
        signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;

        var reference = new Reference(string.Empty) { DigestMethod = SignedXml.XmlDsigSHA256Url };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        signedXml.AddReference(reference);

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(certificate));
        signedXml.KeyInfo = keyInfo;

        signedXml.ComputeSignature();
        document.DocumentElement!.AppendChild(document.ImportNode(signedXml.GetXml(), true));

        return document.OuterXml;
    }
}
