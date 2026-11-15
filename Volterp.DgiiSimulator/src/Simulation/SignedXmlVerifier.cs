using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace Volterp.DgiiSimulator;

/// <summary>
/// The signature of a file the DGII receives. The taxpayer signs with its own
/// certificate and the certificate travels inside the signature, so the
/// simulator checks the signature against the certificate that came with it.
/// </summary>
internal static class SignedXmlVerifier
{
    public static bool TryVerify(string xml, out X509Certificate2? certificate, out string? signatureValue)
    {
        certificate = null;
        signatureValue = null;

        var document = new XmlDocument { PreserveWhitespace = false };
        try
        {
            document.LoadXml(xml);
        }
        catch (XmlException)
        {
            return false;
        }

        var signature = document.DocumentElement?.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl);
        if (signature is null || signature.Count == 0)
            return false;

        var signedXml = new SignedXml(document);
        try
        {
            signedXml.LoadXml((XmlElement)signature[0]!);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }

        var embedded = signedXml.KeyInfo?
            .OfType<KeyInfoX509Data>()
            .SelectMany(data => data.Certificates?.OfType<X509Certificate2>() ?? [])
            .FirstOrDefault();

        if (embedded is null)
            return false;

        try
        {
            if (!signedXml.CheckSignature(embedded, true))
                return false;
        }
        catch (CryptographicException)
        {
            return false;
        }

        certificate = embedded;
        signatureValue = SignatureValueText((XmlElement)signature[0]!);
        return true;
    }

    /// <summary>
    /// The signature value as it travels in the document, which is where the
    /// security code is taken from.
    /// </summary>
    private static string? SignatureValueText(XmlElement signature)
    {
        var value = signature.GetElementsByTagName("SignatureValue", SignedXml.XmlDsigNamespaceUrl);
        return value.Count == 0 ? null : value[0]?.InnerText.Trim();
    }
}
