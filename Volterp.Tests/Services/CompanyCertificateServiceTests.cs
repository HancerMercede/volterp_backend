using System.Text.Json;
using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.CompanyCertificateDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Services;

public class CompanyCertificateServiceTests
{
    private const string IssuerRnc = "130862346";
    private const string Credential = "api-key-of-the-issuer";

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICompanyCertificateRepository> _certificates = new();
    private readonly Mock<ICredentialProtector> _protector = new();

    public CompanyCertificateServiceTests()
    {
        _unitOfWork.Setup(u => u.CompanyCertificates).Returns(_certificates.Object);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _certificates.Setup(r => r.AddAsync(It.IsAny<CompanyCertificate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _protector.Setup(p => p.Protect(It.IsAny<Guid>(), It.IsAny<string>())).Returns("v1:ciphertext");
    }

    [Fact]
    public async Task CreateAsync_PersistsTheCredentialEncrypted()
    {
        CompanyCertificate? persisted = null;
        _certificates.Setup(r => r.AddAsync(It.IsAny<CompanyCertificate>(), It.IsAny<CancellationToken>()))
            .Callback<CompanyCertificate, CancellationToken>((certificate, _) => persisted = certificate)
            .Returns(Task.CompletedTask);

        var service = new CompanyCertificateService(_unitOfWork.Object, _protector.Object);

        var result = await service.CreateAsync(Request(), Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        result.Should().BeOfType<Either<Error, CompanyCertificateDto>.Right>();
        persisted.Should().NotBeNull();
        persisted!.EncryptedCredential.Should().Be("v1:ciphertext");
        persisted.EncryptedCredential.Should().NotContain(Credential);
        _protector.Verify(p => p.Protect(Guid7TestValues.CompanyA, Credential), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNoCredentialMaterial()
    {
        var service = new CompanyCertificateService(_unitOfWork.Object, _protector.Object);

        var result = await service.CreateAsync(Request(), Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        var dto = ((Either<Error, CompanyCertificateDto>.Right)result).Value;
        var serialized = JsonSerializer.Serialize(dto);
        serialized.Should().NotContain(Credential);
        serialized.Should().NotContain("v1:ciphertext");
    }

    [Fact]
    public async Task CreateAsync_NormalizesTheIssuerRncToDigits()
    {
        CompanyCertificate? persisted = null;
        _certificates.Setup(r => r.AddAsync(It.IsAny<CompanyCertificate>(), It.IsAny<CancellationToken>()))
            .Callback<CompanyCertificate, CancellationToken>((certificate, _) => persisted = certificate)
            .Returns(Task.CompletedTask);

        var service = new CompanyCertificateService(_unitOfWork.Object, _protector.Object);

        await service.CreateAsync(Request(taxId: "130-862346"), Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        persisted.Should().NotBeNull();
        persisted!.TaxId.Should().Be(IssuerRnc);
    }

    [Fact]
    public async Task CreateAsync_WithAnInvalidIssuerRnc_ReturnsLeft()
    {
        var service = new CompanyCertificateService(_unitOfWork.Object, _protector.Object);

        var result = await service.CreateAsync(Request(taxId: "123"), Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        result.Should().BeOfType<Either<Error, CompanyCertificateDto>.Left>();
        _certificates.Verify(r => r.AddAsync(It.IsAny<CompanyCertificate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithAnExpirationInThePast_ReturnsLeft()
    {
        var service = new CompanyCertificateService(_unitOfWork.Object, _protector.Object);

        var result = await service.CreateAsync(
            Request(expiresAt: DateTime.UtcNow.AddDays(-1)),
            Guid7TestValues.CompanyA,
            Guid7TestValues.UserA);

        result.Should().BeOfType<Either<Error, CompanyCertificateDto>.Left>();
        _certificates.Verify(r => r.AddAsync(It.IsAny<CompanyCertificate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithoutCredential_ReturnsLeft()
    {
        var service = new CompanyCertificateService(_unitOfWork.Object, _protector.Object);

        var result = await service.CreateAsync(
            Request(credential: " "),
            Guid7TestValues.CompanyA,
            Guid7TestValues.UserA);

        result.Should().BeOfType<Either<Error, CompanyCertificateDto>.Left>();
        _protector.Verify(p => p.Protect(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTheCertificateDoesNotExist_ReturnsLeft()
    {
        _certificates.Setup(r => r.GetByCondictionsAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<CompanyCertificate, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyCertificate?)null);

        var service = new CompanyCertificateService(_unitOfWork.Object, _protector.Object);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeOfType<Either<Error, CompanyCertificateDto>.Left>();
    }

    private static CreateCompanyCertificateDto Request(
        string name = "eCF MSeller",
        string taxId = IssuerRnc,
        DateTime? expiresAt = null,
        string credential = Credential)
        => new(name, taxId, expiresAt ?? DateTime.UtcNow.AddDays(30), credential);
}
