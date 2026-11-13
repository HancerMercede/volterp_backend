using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using FluentAssertions;
using Volterp.ElectronicInvoicing.Signing;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Signing;

// The security code is the first six characters of the SignatureValue. The
// golden is a document signed by our own signer, captured once: it pins the
// rule against a real signature and not against a literal invented here.
public class EcfSecurityCodeTests
{
    private const string GoldenPath = "fixtures/signed-eCF.xml";

    [Fact]
    public void From_TheGoldenDocument_TakesTheFirstSixCharactersOfTheSignature()
    {
        var signed = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, GoldenPath));

        var code = EcfSecurityCode.From(signed);

        code.Should().Be("BoXjC8");
        code.Should().HaveLength(6);
    }

    [Fact]
    public void From_ADocumentSignedNow_TakesTheFirstSixCharactersOfThatSignature()
    {
        using var certificate = CreateCertificate();

        var signed = new XmlDsigEcfSigner().Sign("<ECF Version=\"1.0\"><Encabezado /></ECF>", certificate);

        var code = EcfSecurityCode.From(signed);

        code.Should().Be(SignatureValueOf(signed)[..6]);
    }

    [Fact]
    public void From_ADocumentWithoutSignature_Refuses()
    {
        var act = () => EcfSecurityCode.From("<ECF Version=\"1.0\"><Encabezado /></ECF>");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void From_ASignatureTooShort_Refuses()
    {
        var act = () => EcfSecurityCode.From(
            "<ECF><Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"><SignatureValue>abc</SignatureValue></Signature></ECF>");

        act.Should().Throw<InvalidOperationException>();
    }

    private static string SignatureValueOf(string signedXml)
    {
        var document = new XmlDocument { PreserveWhitespace = false };
        document.LoadXml(signedXml);

        var signature = document.GetElementsByTagName(
            "SignatureValue",
            System.Security.Cryptography.Xml.SignedXml.XmlDsigNamespaceUrl);

        return signature[0]!.InnerText.Trim();
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Volterp Test Emisor, C=DO",
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
}
