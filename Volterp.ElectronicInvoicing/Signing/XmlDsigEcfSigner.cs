using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using Volterp.ElectronicInvoicing.Abstractions;

namespace Volterp.ElectronicInvoicing.Signing;

/// <summary>
/// Signs an e-CF with the issuer's certificate using the XMLDSig profile the
/// DGII requires: an enveloped signature over the whole document, RSA-SHA256
/// as the signature method and SHA-256 as the digest method.
/// </summary>
public sealed class XmlDsigEcfSigner : IEcfSigner
{
    public string Sign(string xml, X509Certificate2 certificate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(certificate);

        if (!certificate.HasPrivateKey)
            throw new CryptographicException("The signing certificate has no private key.");

        var rsa = certificate.GetRSAPrivateKey();
        if (rsa is null)
            throw new CryptographicException("The signing certificate does not carry an RSA private key.");

        // Whitespace is part of what gets digested, so it must survive the round trip.
        var document = new XmlDocument { PreserveWhitespace = true };
        document.LoadXml(xml);

        var signedXml = new SignedXml(document) { SigningKey = rsa };
        signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
        signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;

        // An empty reference URI signs the whole document; the enveloped transform
        // keeps the signature itself out of the digest.
        var reference = new Reference(string.Empty) { DigestMethod = SignedXml.XmlDsigSHA256Url };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        signedXml.AddReference(reference);

        // The receiver has to be able to validate the signature without knowing
        // the issuer beforehand, so the certificate travels inside the signature.
        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(certificate));
        signedXml.KeyInfo = keyInfo;

        signedXml.ComputeSignature();

        var signature = signedXml.GetXml();
        document.DocumentElement!.AppendChild(document.ImportNode(signature, true));

        var signedDocument = document.OuterXml;

        if (!Verify(signedDocument, certificate))
            throw new CryptographicException("The generated signature did not verify against the signing certificate.");

        return signedDocument;
    }

    public bool Verify(string signedXml, X509Certificate2 certificate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signedXml);
        ArgumentNullException.ThrowIfNull(certificate);

        var document = new XmlDocument { PreserveWhitespace = true };
        document.LoadXml(signedXml);

        var signature = document.DocumentElement?.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl);
        if (signature is null || signature.Count == 0)
            return false;

        var verifier = new SignedXml(document);
        verifier.LoadXml((XmlElement)signature[0]!);

        return verifier.CheckSignature(certificate, true);
    }
}
