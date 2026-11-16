using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EitherWay;
using FluentAssertions;
using MapFlow;
using Microsoft.EntityFrameworkCore;
using Moq;
using Volterp.Application.DTOs.ReceivedElectronicInvoiceDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Services;

public class ElectronicInvoiceReceptionServiceTests
{
    private const string IssuerRnc = "130862346";
    private const string ReceiverRnc = "130862354";
    private const string Encf = "E310000000001";
    private const string RejectionReason = "The commercial terms do not match.";

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IReceivedElectronicInvoiceRepository> _received = new();

    public ElectronicInvoiceReceptionServiceTests()
    {
        _unitOfWork.SetupGet(u => u.Companies).Returns(_companies.Object);
        _unitOfWork.SetupGet(u => u.ReceivedElectronicInvoices).Returns(_received.Object);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        GivenCompany(ReceiverRnc, Guid7TestValues.CompanyA);
        _received
            .Setup(r => r.GetByIssuerAndEncfAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReceivedElectronicInvoice?)null);
    }

    [Fact]
    public async Task ReceiveAsync_WithUnknownReceiver_ReturnsLeftAndStoresNothing()
    {
        GivenCompanies([], 0);
        var service = Service();

        var result = await service.ReceiveAsync(
            ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
            ReceptionTestDocuments.FileName(ReceiverRnc, Encf));

        result.Should().BeOfType<Either<Error, ReceivedElectronicInvoiceDto>.Left>();
        var error = ((Either<Error, ReceivedElectronicInvoiceDto>.Left)result).Value;
        error.Message.Should().Contain("unknown");
        _received.Verify(
            r => r.AddAsync(It.IsAny<ReceivedElectronicInvoice>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReceiveAsync_WithMalformedXml_ReturnsLeftAndStoresNothing()
    {
        var service = Service();

        var result = await service.ReceiveAsync(
            "<ECF><Encabezado>",
            ReceptionTestDocuments.FileName(ReceiverRnc, Encf));

        result.Should().BeOfType<Either<Error, ReceivedElectronicInvoiceDto>.Left>();
        var error = ((Either<Error, ReceivedElectronicInvoiceDto>.Left)result).Value;
        error.Message.Should().Contain("malformed");
        _received.Verify(
            r => r.AddAsync(It.IsAny<ReceivedElectronicInvoice>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReceiveAsync_AttributesTheTenantFromTheReceiverRnc()
    {
        var service = Service();

        var result = await service.ReceiveAsync(
            ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
            ReceptionTestDocuments.FileName(ReceiverRnc, Encf));

        result.Should().BeOfType<Either<Error, ReceivedElectronicInvoiceDto>.Right>();
        var invoice = ((Either<Error, ReceivedElectronicInvoiceDto>.Right)result).Value;
        invoice.CompanyId.Should().Be(Guid7TestValues.CompanyA);
        invoice.ReceiverTaxId.Should().Be(ReceiverRnc);
        invoice.IssuerTaxId.Should().Be(IssuerRnc);
        invoice.ENCF.Should().Be(Encf);
        invoice.AlreadyReceived.Should().BeFalse();
    }

    [Fact]
    public async Task ReceiveAsync_WithAnAmbiguousReceiverRnc_ReturnsLeft()
    {
        GivenCompanies(
            [
                Company(Guid7TestValues.CompanyA, ReceiverRnc),
                Company(Guid7TestValues.CompanyB, ReceiverRnc)
            ],
            2);
        var service = Service();

        var result = await service.ReceiveAsync(
            ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
            ReceptionTestDocuments.FileName(ReceiverRnc, Encf));

        result.Should().BeOfType<Either<Error, ReceivedElectronicInvoiceDto>.Left>();
        var error = ((Either<Error, ReceivedElectronicInvoiceDto>.Left)result).Value;
        error.Message.Should().Contain("ambiguous");
        _received.Verify(
            r => r.AddAsync(It.IsAny<ReceivedElectronicInvoice>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReceiveAsync_TwiceWithTheSameIssuerAndEncf_DoesNotDuplicate()
    {
        var stored = new ReceivedElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = Guid7TestValues.CompanyA,
            IssuerTaxId = IssuerRnc,
            ReceiverTaxId = ReceiverRnc,
            ENCF = Encf,
            DocumentType = Volterp.ElectronicInvoicing.EcfDocumentType.FiscalCredit,
            SignedXml = ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
            SignedAt = new DateTime(2026, 11, 15, 10, 30, 0, DateTimeKind.Utc),
            ReceivedAt = new DateTime(2026, 11, 15, 10, 31, 0, DateTimeKind.Utc)
        };

        _received
            .SetupSequence(r => r.GetByIssuerAndEncfAsync(
                IssuerRnc,
                Encf,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReceivedElectronicInvoice?)null)
            .ReturnsAsync(stored);

        var service = Service();
        var xml = ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf);
        var fileName = ReceptionTestDocuments.FileName(ReceiverRnc, Encf);

        var first = await service.ReceiveAsync(xml, fileName);
        var second = await service.ReceiveAsync(xml, fileName);

        first.Should().BeOfType<Either<Error, ReceivedElectronicInvoiceDto>.Right>();
        second.Should().BeOfType<Either<Error, ReceivedElectronicInvoiceDto>.Right>();
        ((Either<Error, ReceivedElectronicInvoiceDto>.Right)first).Value.AlreadyReceived.Should().BeFalse();
        ((Either<Error, ReceivedElectronicInvoiceDto>.Right)second).Value.AlreadyReceived.Should().BeTrue();
        _received.Verify(
            r => r.AddAsync(It.IsAny<ReceivedElectronicInvoice>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReceiveAsync_WhenTheNaturalKeyBelongsToAnotherTenant_ReturnsGenericConflictAndStoresNothing()
    {
        var stored = new ReceivedElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = Guid7TestValues.CompanyB,
            IssuerTaxId = IssuerRnc,
            ReceiverTaxId = ReceiverRnc,
            ENCF = Encf,
            DocumentType = Volterp.ElectronicInvoicing.EcfDocumentType.FiscalCredit,
            SignedXml = ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
            SignedAt = new DateTime(2026, 11, 15, 10, 30, 0, DateTimeKind.Utc),
            ReceivedAt = new DateTime(2026, 11, 15, 10, 31, 0, DateTimeKind.Utc)
        };

        _received
            .Setup(r => r.GetByIssuerAndEncfAsync(
                IssuerRnc,
                Encf,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        var service = Service();

        var result = await service.ReceiveAsync(
            ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
            ReceptionTestDocuments.FileName(ReceiverRnc, Encf));

        result.Should().BeOfType<Either<Error, ReceivedElectronicInvoiceDto>.Left>();
        var error = ((Either<Error, ReceivedElectronicInvoiceDto>.Left)result).Value;
        error.Message.Should().Be("The e-CF was already received.");
        error.Message.Should().NotContain("tenant");
        _received.Verify(
            r => r.AddAsync(It.IsAny<ReceivedElectronicInvoice>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DecideCommercialApprovalAsync_RecordsWhoWhenAndWhy()
    {
        var invoice = new ReceivedElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = Guid7TestValues.CompanyA,
            IssuerTaxId = IssuerRnc,
            ReceiverTaxId = ReceiverRnc,
            ENCF = Encf,
            DocumentType = Volterp.ElectronicInvoicing.EcfDocumentType.FiscalCredit,
            SignedXml = ReceptionTestDocuments.ReceivedEcf(IssuerRnc, ReceiverRnc, Encf),
            SignedAt = new DateTime(2026, 11, 15, 10, 30, 0, DateTimeKind.Utc),
            ReceivedAt = new DateTime(2026, 11, 15, 10, 31, 0, DateTimeKind.Utc)
        };

        _received
            .Setup(r => r.GetByCompanyAndIdAsync(
                Guid7TestValues.CompanyA,
                invoice.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var service = Service();

        var result = await service.DecideCommercialApprovalAsync(
            invoice.Id,
            Guid7TestValues.CompanyA,
            Guid7TestValues.UserA,
            approved: false,
            RejectionReason);

        result.Should().BeOfType<Either<Error, ReceivedElectronicInvoiceDto?>.Right>();
        var dto = ((Either<Error, ReceivedElectronicInvoiceDto?>.Right)result).Value;
        dto!.ApprovalStatus.Should().Be(ReceivedElectronicInvoiceApprovalStatus.Rejected);
        dto.ApprovalDecidedBy.Should().Be(Guid7TestValues.UserA.ToString());
        dto.ApprovalDecidedAt.Should().NotBeNull();
        dto.ApprovalReason.Should().Be(RejectionReason);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DecideCommercialApprovalAsync_WhenTheDocumentDoesNotExist_ReturnsTheAbsenceOfTheAnswer()
    {
        var invoiceId = Guid.CreateVersion7();

        _received
            .Setup(r => r.GetByCompanyAndIdAsync(
                Guid7TestValues.CompanyA,
                invoiceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReceivedElectronicInvoice?)null);

        var service = Service();

        var result = await service.DecideCommercialApprovalAsync(
            invoiceId,
            Guid7TestValues.CompanyA,
            Guid7TestValues.UserA,
            approved: true,
            "Approved by the buyer");

        // The document that does not exist is the absence of the answer, not a
        // failure: the caller tells it apart without reading a message.
        result.Should().BeOfType<Either<Error, ReceivedElectronicInvoiceDto?>.Right>();
        ((Either<Error, ReceivedElectronicInvoiceDto?>.Right)result).Value.Should().BeNull();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReceiveCommercialApprovalAsync_ForAnotherDocument_ReturnsLeft()
    {
        _received
            .Setup(r => r.GetByIssuerAndEncfAsync(
                "101500018",
                Encf,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReceivedElectronicInvoice?)null);

        var service = Service();

        var result = await service.ReceiveCommercialApprovalAsync(
            ReceptionTestDocuments.CommercialApproval(
                "101500018",
                ReceiverRnc,
                Encf,
                approved: true,
                reason: "Accepted"),
            ReceptionTestDocuments.FileName(ReceiverRnc, Encf));

        result.Should().BeOfType<Either<Error, ReceivedElectronicInvoiceDto>.Left>();
        var error = ((Either<Error, ReceivedElectronicInvoiceDto>.Left)result).Value;
        error.Message.Should().Contain("does not correspond");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private ElectronicInvoiceReceptionService Service()
        => new(_unitOfWork.Object);

    private void GivenCompany(string taxId, Guid companyId)
        => GivenCompanies([Company(companyId, taxId)], 1);

    private void GivenCompanies(IReadOnlyList<Company> companies, int rowCount)
    {
        _companies
            .Setup(c => c.GetAllAsync(
                It.IsAny<Expression<Func<Company, bool>>>(),
                1,
                2,
                It.IsAny<CancellationToken>(),
                It.IsAny<Func<IQueryable<Company>, IQueryable<Company>>>() ))
            .ReturnsAsync(new PagedResult<Company>
            {
                RowCount = rowCount,
                PageNumber = 1,
                PageSize = 2,
                PageCount = rowCount == 0 ? 0 : 1,
                Items = companies.ToList()
            });
    }

    private static Company Company(Guid id, string taxId)
        => new()
        {
            Id = id,
            Name = $"Company {id:N}",
            LegalName = $"Company {id:N}",
            TaxId = taxId,
            Address = "1 Main Street",
            Email = $"company-{id:N}@test.local",
            Phone = "809-000-0000",
            IsActive = true
        };
}

internal static class ReceptionTestDocuments
{
    public static string FileName(string receiverRnc, string encf)
        => $"{receiverRnc}{encf}.xml";

    public static string ReceivedEcf(string issuerRnc, string receiverRnc, string encf)
        => Sign($"""
            <ECF>
              <Encabezado>
                <IdDoc>
                  <TipoeCF>31</TipoeCF>
                  <eNCF>{encf}</eNCF>
                </IdDoc>
                <Emisor>
                  <RNCEmisor>{issuerRnc}</RNCEmisor>
                </Emisor>
                <Comprador>
                  <RNCComprador>{receiverRnc}</RNCComprador>
                </Comprador>
              </Encabezado>
              <FechaHoraFirma>15-11-2026 10:30:00</FechaHoraFirma>
            </ECF>
            """);

    public static string CommercialApproval(
        string issuerRnc,
        string receiverRnc,
        string encf,
        bool approved,
        string reason)
        => Sign($"""
            <ACECF>
              <DetalleAprobacionComercial>
                <RNCEmisor>{issuerRnc}</RNCEmisor>
                <eNCF>{encf}</eNCF>
                <RNCComprador>{receiverRnc}</RNCComprador>
                <Estado>{(approved ? 1 : 2)}</Estado>
                <DetalleMotivoRechazo>{reason}</DetalleMotivoRechazo>
                <FechaHoraAprobacionComercial>15-11-2026 11:00:00</FechaHoraAprobacionComercial>
              </DetalleAprobacionComercial>
            </ACECF>
            """);

    private static string Sign(string xml)
    {
        using var certificate = CreateCertificate();
        return new XmlDsigEcfSigner().Sign(xml, certificate);
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Volterp Reception Test, C=DO",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));

        return X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pfx, "reception-test-password"),
            "reception-test-password",
            X509KeyStorageFlags.EphemeralKeySet);
    }
}
