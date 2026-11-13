using System.Security.Cryptography.Xml;
using System.Xml;

namespace Volterp.ElectronicInvoicing.Signing;

// The security code the taxpayer prints next to the QR: the first six
// characters of the SignatureValue of the signed document. The citizen
// reads it on the receipt and the DGII matches it against the query.
public static class EcfSecurityCode
{
    private const int Length = 6;

    public static string From(string signedXml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signedXml);

        var document = new XmlDocument { PreserveWhitespace = false };
        document.LoadXml(signedXml);

        var signatures = document.GetElementsByTagName("SignatureValue", SignedXml.XmlDsigNamespaceUrl);

        if (signatures.Count == 0)
            throw new InvalidOperationException("The document carries no signature to take the security code from.");

        var signature = signatures[0]?.InnerText.Trim() ?? string.Empty;

        if (signature.Length < Length)
            throw new InvalidOperationException("The signature is too short to carry a security code.");

        return signature[..Length];
    }
}
