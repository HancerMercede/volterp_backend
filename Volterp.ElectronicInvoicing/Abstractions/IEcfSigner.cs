using System.Security.Cryptography.X509Certificates;

namespace Volterp.ElectronicInvoicing.Abstractions;

/// <summary>
/// Signs an e-CF document with the issuer's certificate and verifies the result.
/// </summary>
public interface IEcfSigner
{
    /// <summary>
    /// Signs the e-CF XML and returns the signed document. The input is never
    /// modified and the signature is verified before it is returned, so a
    /// document that comes back from here is always verifiable.
    /// </summary>
    string Sign(string xml, X509Certificate2 certificate);

    /// <summary>
    /// Verifies that the signed XML carries a valid signature made with the
    /// given certificate.
    /// </summary>
    bool Verify(string signedXml, X509Certificate2 certificate);
}
