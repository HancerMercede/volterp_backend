using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using FluentAssertions;
using Volterp.ElectronicInvoicing.Signing;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Signing;

public class XmlDsigEcfSignerTests
{
    private const string Password = "volterp-test-password";

    private const string Ecf = """
        <?xml version="1.0" encoding="utf-8"?>
        <e-CF Version="1.0">
          <Encabezado>
            <RNCEmisor>101000001</RNCEmisor>
            <TipoDocElectronico>31</TipoDocElectronico>
            <eNCF>E310000000001</eNCF>
          </Encabezado>
          <Detalle>
            <Linea>
              <NumeroLinea>1</NumeroLinea>
              <Descripcion>Producto de prueba</Descripcion>
              <MontoTotal>100.00</MontoTotal>
            </Linea>
          </Detalle>
        </e-CF>
        """;

    private readonly XmlDsigEcfSigner _signer = new();

    [Fact]
    public void Sign_AddsTheSignatureToTheDocument()
    {
        using var certificate = CreateCertificate();

        var signed = _signer.Sign(Ecf, certificate);

        var document = new XmlDocument();
        document.LoadXml(signed);
        var signature = document.DocumentElement!.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl);

        signature.Count.Should().Be(1);
        document.DocumentElement!.LastChild!.Name.Should().Be("Signature");
    }

    [Fact]
    public void Sign_UsesTheProfileTheDgiiRequires()
    {
        using var certificate = CreateCertificate();

        var signed = _signer.Sign(Ecf, certificate);

        var signedXml = LoadSignedXml(signed);
        signedXml.SignedInfo!.SignatureMethod.Should().Be(SignedXml.XmlDsigRSASHA256Url);
        signedXml.SignedInfo.CanonicalizationMethod.Should().Be(SignedXml.XmlDsigC14NTransformUrl);

        var reference = signedXml.SignedInfo.References!.Cast<Reference>().Single();
        reference.DigestMethod.Should().Be(SignedXml.XmlDsigSHA256Url);
        reference.Uri.Should().Be(string.Empty);
    }

    [Fact]
    public void Sign_UsesAnEnvelopedSignature()
    {
        using var certificate = CreateCertificate();

        var signed = _signer.Sign(Ecf, certificate);

        var signedXml = LoadSignedXml(signed);
        var reference = signedXml.SignedInfo!.References!.Cast<Reference>().Single();

        reference.TransformChain.Count.Should().Be(1);
        reference.TransformChain[0].Should().BeOfType<XmlDsigEnvelopedSignatureTransform>();
    }

    [Fact]
    public void Sign_ProducesASignatureThatVerifiesWithTheIssuerCertificate()
    {
        using var certificate = CreateCertificate();

        var signed = _signer.Sign(Ecf, certificate);

        _signer.Verify(signed, certificate).Should().BeTrue();
    }

    [Fact]
    public void Sign_KeepsTheContentOfTheOriginalDocument()
    {
        using var certificate = CreateCertificate();

        var signed = _signer.Sign(Ecf, certificate);

        signed.Should().Contain("<eNCF>E310000000001</eNCF>");
        signed.Should().Contain("<MontoTotal>100.00</MontoTotal>");
    }

    [Fact]
    public void Sign_LeavesTheOriginalXmlUnsigned()
    {
        using var certificate = CreateCertificate();

        _signer.Sign(Ecf, certificate);

        Ecf.Should().NotContain("Signature");
    }

    [Fact]
    public void Sign_ThrowsWhenTheCertificateHasNoPrivateKey()
    {
        using var certificate = CreateCertificate();
        using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));

        var act = () => _signer.Sign(Ecf, publicOnly);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Verify_ReturnsFalseWhenTheSignedContentWasChanged()
    {
        using var certificate = CreateCertificate();
        var signed = _signer.Sign(Ecf, certificate);

        var tampered = signed.Replace("100.00", "900.00");

        _signer.Verify(tampered, certificate).Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalseWhenTheDocumentWasSignedWithAnotherCertificate()
    {
        using var issuer = CreateCertificate();
        using var other = CreateCertificate("Otro Emisor");
        var signed = _signer.Sign(Ecf, issuer);

        _signer.Verify(signed, other).Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalseWhenTheDocumentHasNoSignature()
    {
        using var certificate = CreateCertificate();

        _signer.Verify(Ecf, certificate).Should().BeFalse();
    }

    private static SignedXml LoadSignedXml(string signed)
    {
        var document = new XmlDocument { PreserveWhitespace = true };
        document.LoadXml(signed);

        var signature = document.DocumentElement!.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl);
        var signedXml = new SignedXml(document);
        signedXml.LoadXml((XmlElement)signature[0]!);
        return signedXml;
    }

    private static X509Certificate2 CreateCertificate(string commonName = "Volterp Test Emisor")
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
