using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Moq;
using Volterp.Application.Credentials;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Services;
using Xunit;

namespace Volterp.Tests.Infrastructure;

public class CompanySigningCertificateProviderTests
{
    private const string IssuerRnc = "130862346";
    private const string Password = "volterp-test-password";

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICompanyCertificateRepository> _certificates = new();
    private readonly Guid _companyId = Guid.CreateVersion7();
    private readonly string _masterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public CompanySigningCertificateProviderTests()
    {
        _unitOfWork.Setup(u => u.CompanyCertificates).Returns(_certificates.Object);
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithARegisteredCertificate_ReturnsTheCertificateOfTheIssuer()
    {
        GivenCertificate(CreatePfx(), Password);

        var provider = CreateProvider();
        using var certificate = await provider.GetSigningCertificateAsync(IssuerRnc);

        certificate.Subject.Should().Contain("Volterp Test Emisor");
        certificate.HasPrivateKey.Should().BeTrue();
    }

    [Fact]
    public async Task GetSigningCertificateAsync_AsksForTheIssuerRncInDigits()
    {
        GivenCertificate(CreatePfx(), Password);

        var provider = CreateProvider();
        using var certificate = await provider.GetSigningCertificateAsync("1-30-86234-6");

        certificate.Should().NotBeNull();
        _certificates.Verify(r => r.GetActiveByTaxIdAsync(IssuerRnc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithoutARegisteredCertificate_Throws()
    {
        _certificates.Setup(r => r.GetActiveByTaxIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyCertificate?)null);

        var provider = CreateProvider();

        var act = () => provider.GetSigningCertificateAsync(IssuerRnc);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithAnInvalidRnc_ThrowsWithoutLookingForTheCertificate()
    {
        var provider = CreateProvider();

        var act = () => provider.GetSigningCertificateAsync("123");

        await act.Should().ThrowAsync<InvalidOperationException>();
        _certificates.Verify(r => r.GetActiveByTaxIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithAWrongPassword_Throws()
    {
        GivenCertificate(CreatePfx(), "another-password");

        var provider = CreateProvider();

        var act = () => provider.GetSigningCertificateAsync(IssuerRnc);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithAnExpiredCertificate_Throws()
    {
        // The row says it is valid for 30 days; the certificate inside says it
        // expired yesterday. Only the certificate decides.
        GivenCertificate(CreatePfx(notAfter: DateTimeOffset.UtcNow.AddDays(-1)), Password);

        var provider = CreateProvider();

        var act = () => provider.GetSigningCertificateAsync(IssuerRnc);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithACredentialOfAnotherCompany_Throws()
    {
        var protector = new AesCredentialProtector(_masterKey);
        var envelope = new SigningCredential(CreatePfx(), Password).ToEnvelope();
        GivenCertificate(protector.Protect(Guid.CreateVersion7(), envelope));

        var provider = CreateProvider();

        var act = () => provider.GetSigningCertificateAsync(IssuerRnc);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithACredentialThatIsNotTheSigningMaterial_Throws()
    {
        var protector = new AesCredentialProtector(_masterKey);
        GivenCertificate(protector.Protect(_companyId, "api-key-of-the-issuer"));

        var provider = CreateProvider();

        var act = () => provider.GetSigningCertificateAsync(IssuerRnc);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private CompanySigningCertificateProvider CreateProvider()
        => new(_unitOfWork.Object, new AesCredentialProtector(_masterKey));

    private void GivenCertificate(string pfxBase64, string password)
    {
        var protector = new AesCredentialProtector(_masterKey);
        GivenCertificate(protector.Protect(_companyId, new SigningCredential(pfxBase64, password).ToEnvelope()));
    }

    private void GivenCertificate(string encryptedCredential)
    {
        var certificate = new CompanyCertificate
        {
            Id = Guid.CreateVersion7(),
            CompanyId = _companyId,
            Name = "eCF",
            TaxId = IssuerRnc,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            Status = EntityStatus.Active,
            EncryptedCredential = encryptedCredential
        };

        _certificates.Setup(r => r.GetActiveByTaxIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(certificate);
    }

    private static string CreatePfx(string password = Password, DateTimeOffset? notAfter = null)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Volterp Test Emisor, C=DO",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var expiresAt = notAfter ?? DateTimeOffset.UtcNow.AddDays(365);
        using var certificate = request.CreateSelfSigned(
            IssuedAt(expiresAt),
            expiresAt);

        return Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, password));
    }

    private static DateTimeOffset IssuedAt(DateTimeOffset expiresAt)
    {
        var issuedAt = DateTimeOffset.UtcNow.AddDays(-30);

        return issuedAt < expiresAt ? issuedAt : expiresAt.AddDays(-1);
    }
}
