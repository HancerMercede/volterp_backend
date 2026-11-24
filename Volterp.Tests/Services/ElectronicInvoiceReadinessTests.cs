using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.ElectronicInvoiceDtos;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Abstractions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Xml;
using Xunit;

namespace Volterp.Tests.Services;

// The readiness of the taxpayer: what it needs to have to issue right now.
// The front used to deduce it from three screens, and the user that cannot
// read the difference pays for it with a document the DGII rejects.
//
// The system is modular: the taxpayer that does not want the electronic
// invoicing bills normally with the same system, so the mode is not a
// blocker. The readiness follows the path the sale takes right now.
public class ElectronicInvoiceReadinessTests
{
    private static readonly Guid CompanyId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d6e");
    private const string IssuerRnc = "10100000101";

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<ICompanyCertificateRepository> _certificates = new();
    private readonly Mock<IElectronicInvoiceSequenceRepository> _sequences = new();
    private readonly Mock<INormalFiscalSequenceRepository> _normalSequences = new();
    private readonly Mock<IEcfSigner> _signer = new();
    private readonly Mock<ISigningCertificateProvider> _certificateProvider = new();
    private readonly Mock<IDgiiClient> _dgii = new();

    private readonly List<ElectronicInvoiceSequence> _ranges = [];
    private readonly List<NormalFiscalSequence> _normalRanges = [];
    private readonly DateTime _now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    public ElectronicInvoiceReadinessTests()
    {
        _unitOfWork.Setup(u => u.Companies).Returns(_companies.Object);
        _unitOfWork.Setup(u => u.CompanyCertificates).Returns(_certificates.Object);
        _unitOfWork.Setup(u => u.ElectronicInvoiceSequences).Returns(_sequences.Object);
        _unitOfWork.Setup(u => u.NormalFiscalSequences).Returns(_normalSequences.Object);

        _sequences.Setup(s => s.GetRangesAsync(CompanyId, IssuerRnc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _ranges.ToList());

        _normalSequences.Setup(s => s.GetRangesAsync(CompanyId, IssuerRnc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _normalRanges.ToList());
    }

    [Fact]
    public async Task GetReadinessAsync_SaysFromWhichAmountTheDocumentHasToNameItsBuyer()
    {
        // The rule of the DGII: from RD$250,000 the consumption invoice is
        // nominative and has to name its buyer. The front asks for the tax
        // id before the cashier completes the sale, so the amount travels in
        // the answer and the front does not carry a number of its own.
        GivenCompany(CompanyEmissionMode.Electronic);
        GivenCertificate(expiresAt: _now.AddDays(30));
        GivenRange(EcfDocumentType.Consumer, from: 1, to: 100, current: 20);

        var readiness = await ReadinessAsync();

        readiness.Should().NotBeNull();
        readiness!.BuyerTaxIdRequiredFrom.Should().Be(250000m);
    }

    [Fact]
    public async Task GetReadinessAsync_WithACertificateAndARangeWithRoom_SaysTheTaxpayerIsReady()
    {
        GivenCompany(CompanyEmissionMode.Electronic);
        GivenCertificate(expiresAt: _now.AddDays(30));
        GivenRange(EcfDocumentType.Consumer, from: 1, to: 100, current: 20);

        var readiness = await ReadinessAsync();

        readiness.Should().NotBeNull();
        readiness!.IsReady.Should().BeTrue();
        readiness.UsesElectronicEmission.Should().BeTrue();
        readiness.EmissionMode.Should().Be("Electronic");
        readiness.IsInContingency.Should().BeFalse();
        readiness.HasValidCertificate.Should().BeTrue();
        readiness.CertificateExpiresAt.Should().Be(_now.AddDays(30));
        readiness.Blockers.Should().BeEmpty();

        var consumer = readiness.DocumentTypes.Single(x => x.Code == "E32");
        consumer.IsIssuable.Should().BeTrue();
        consumer.Remaining.Should().Be(80);
        consumer.Blocker.Should().BeNull();
    }

    [Fact]
    public async Task GetReadinessAsync_WithoutACertificate_SaysTheTaxpayerIsNotReadyAndNamesTheBlocker()
    {
        GivenCompany(CompanyEmissionMode.Electronic);
        GivenRange(EcfDocumentType.Consumer, from: 1, to: 100, current: 0);

        var readiness = await ReadinessAsync();

        readiness.Should().NotBeNull();
        readiness!.IsReady.Should().BeFalse();
        readiness.HasValidCertificate.Should().BeFalse();
        readiness.CertificateExpiresAt.Should().BeNull();
        readiness.Blockers.Should().ContainSingle(blocker => blocker.Code == "certificate-missing");
    }

    [Fact]
    public async Task GetReadinessAsync_WhenTheRangeOfTheConsumptionIsExhausted_SaysThatTypeIsNotIssuable()
    {
        GivenCompany(CompanyEmissionMode.Electronic);
        GivenCertificate(expiresAt: _now.AddDays(30));
        GivenRange(EcfDocumentType.Consumer, from: 1, to: 100, current: 100);

        var readiness = await ReadinessAsync();

        readiness.Should().NotBeNull();
        readiness!.IsReady.Should().BeFalse();

        var consumer = readiness.DocumentTypes.Single(x => x.Code == "E32");
        consumer.IsIssuable.Should().BeFalse();
        consumer.Remaining.Should().Be(0);
        consumer.Blocker.Should().Contain("exhausted");
        consumer.BlockerCode.Should().Be("range-exhausted");
    }

    [Fact]
    public async Task GetReadinessAsync_WhenTheRangeExpired_SaysThatTypeIsNotIssuable()
    {
        GivenCompany(CompanyEmissionMode.Electronic);
        GivenCertificate(expiresAt: _now.AddDays(30));
        GivenRange(EcfDocumentType.FiscalCredit, from: 1, to: 100, current: 0, expiresAt: _now.AddDays(-1));
        GivenRange(EcfDocumentType.Consumer, from: 1, to: 100, current: 0);

        var readiness = await ReadinessAsync();

        readiness.Should().NotBeNull();

        var credit = readiness!.DocumentTypes.Single(x => x.Code == "E31");
        credit.IsIssuable.Should().BeFalse();
        credit.Blocker.Should().Contain("expired on");
        credit.BlockerCode.Should().Be("range-expired");
    }

    [Fact]
    public async Task GetReadinessAsync_WhenTheRangeWasNotUsedYet_CountsEveryNumberOfIt()
    {
        GivenCompany(CompanyEmissionMode.Electronic);
        GivenCertificate(expiresAt: _now.AddDays(30));
        GivenRange(EcfDocumentType.Consumer, from: 100, to: 199, current: 0);

        var readiness = await ReadinessAsync();

        var consumer = readiness!.DocumentTypes.Single(x => x.Code == "E32");
        consumer.IsIssuable.Should().BeTrue();
        consumer.Remaining.Should().Be(100);
    }

    [Fact]
    public async Task GetReadinessAsync_WhenTheTaxpayerInvoicesInTheNormalMode_SaysItIsReadyWithTheNcfTypes()
    {
        GivenCompany(CompanyEmissionMode.Normal);
        // The taxpayer that bills on paper does not need a certificate: the
        // blocker would be noise.
        GivenNormalRange(NormalFiscalDocumentType.Consumer, from: 1, to: 500, current: 100);

        var readiness = await ReadinessAsync();

        readiness.Should().NotBeNull();
        readiness!.IsReady.Should().BeTrue();
        readiness.UsesElectronicEmission.Should().BeFalse();
        readiness.EmissionMode.Should().Be("Normal");
        readiness.IsInContingency.Should().BeFalse();
        readiness.Blockers.Should().BeEmpty();

        readiness.DocumentTypes.Select(x => x.Code)
            .Should().BeEquivalentTo(["B01", "B02", "B03", "B04"]);

        var consumption = readiness.DocumentTypes.Single(x => x.Code == "B02");
        consumption.IsIssuable.Should().BeTrue();
        consumption.Remaining.Should().Be(400);
    }

    [Fact]
    public async Task GetReadinessAsync_WhenTheTaxpayerInTheNormalModeHasNoNcfRange_SaysItIsNotReadyAndNamesTheBlocker()
    {
        GivenCompany(CompanyEmissionMode.Normal);

        var readiness = await ReadinessAsync();

        readiness.Should().NotBeNull();
        readiness!.IsReady.Should().BeFalse();
        readiness.Blockers.Should().ContainSingle(blocker => blocker.Code == "range-missing");
        readiness.Blockers[0].Message.Should().Contain("NCF").And.Contain("B02");
        readiness.Blockers[0].DocumentTypeCode.Should().Be("B02");
    }

    [Fact]
    public async Task GetReadinessAsync_WhenTheTaxpayerIsInsideTheContingency_SaysItIssuesOnPaper()
    {
        GivenCompany(
            CompanyEmissionMode.Electronic,
            CompanyContingencyStatus.Total,
            startsAt: _now.AddDays(-1),
            endsAt: _now.AddDays(1));
        GivenNormalRange(NormalFiscalDocumentType.Consumer, from: 1, to: 500, current: 100);

        var readiness = await ReadinessAsync();

        readiness.Should().NotBeNull();
        readiness!.IsReady.Should().BeTrue();
        readiness.IsInContingency.Should().BeTrue();
        readiness.UsesElectronicEmission.Should().BeFalse();

        // The taxpayer inside a contingency issues on paper: the checklist is
        // the one of the NCF.
        readiness.DocumentTypes.Should().NotBeEmpty();
        readiness.DocumentTypes.Should().OnlyContain(x => x.Code.StartsWith("B"));
    }

    [Fact]
    public async Task GetReadinessAsync_WhenTheCompanyDoesNotExist_FailsWithTheError()
    {
        _companies.Setup(c => c.GetCompanyByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var result = await Service().GetReadinessAsync(CompanyId);

        var error = result.Match(failure => failure.Message, _ => string.Empty);
        error.Should().Be("The company does not exist.");
    }

    [Fact]
    public async Task GetReadinessAsync_WithoutAnId_FailsWithTheError()
    {
        var result = await Service().GetReadinessAsync(Guid.Empty);

        var error = result.Match(failure => failure.Message, _ => string.Empty);
        error.Should().Be("companyId must not be empty.");
    }

    private async Task<FiscalReadinessDto?> ReadinessAsync()
    {
        var result = await Service().GetReadinessAsync(CompanyId);

        return result.Match(_ => (FiscalReadinessDto?)null, readiness => readiness);
    }

    private void GivenCompany(
        CompanyEmissionMode mode,
        CompanyContingencyStatus contingency = CompanyContingencyStatus.None,
        DateTime? startsAt = null,
        DateTime? endsAt = null)
    {
        var company = new Company
        {
            Id = CompanyId,
            Name = "Company",
            TaxId = IssuerRnc,
            EmissionMode = mode,
            ContingencyStatus = contingency,
            ContingencyStartsAt = startsAt,
            ContingencyEndsAt = endsAt
        };

        _companies.Setup(c => c.GetCompanyByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
    }

    private void GivenCertificate(DateTime expiresAt)
    {
        var certificate = new CompanyCertificate
        {
            Id = Guid.CreateVersion7(),
            CompanyId = CompanyId,
            TaxId = IssuerRnc,
            ExpiresAt = expiresAt
        };

        _certificates.Setup(c => c.GetActiveByTaxIdAsync(IssuerRnc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(certificate);
    }

    private void GivenRange(
        EcfDocumentType documentType,
        int from,
        int to,
        int current,
        DateTime? expiresAt = null)
    {
        _ranges.Add(new ElectronicInvoiceSequence
        {
            Id = Guid.CreateVersion7(),
            CompanyId = CompanyId,
            IssuerTaxId = IssuerRnc,
            DocumentType = documentType,
            From = from,
            To = to,
            CurrentSequence = current,
            ExpiresAt = expiresAt
        });
    }

    private void GivenNormalRange(
        NormalFiscalDocumentType documentType,
        int from,
        int to,
        int current,
        DateTime? expiresAt = null)
    {
        _normalRanges.Add(new NormalFiscalSequence
        {
            Id = Guid.CreateVersion7(),
            CompanyId = CompanyId,
            IssuerTaxId = IssuerRnc,
            DocumentType = documentType,
            From = from,
            To = to,
            CurrentSequence = current,
            ExpiresAt = expiresAt
        });
    }

    private ElectronicInvoiceService Service()
    {
        var authenticator = new DgiiAuthenticator(_dgii.Object, _signer.Object, new DgiiTokenCache());

        return new ElectronicInvoiceService(
            _unitOfWork.Object,
            new EcfXmlWriter(),
            _signer.Object,
            _certificateProvider.Object,
            authenticator,
            _dgii.Object,
            timeProvider: new FixedTimeProvider(_now));
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }
}
