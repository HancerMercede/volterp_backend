using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EitherWay;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Abstractions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.UnitOfWork;
using Xunit;

namespace Volterp.Tests.Integration;

// The annulment of a range over the real stack: the repository, the writer of
// the ANECF, the signer and the transport. The numbers that the taxpayer
// never used are announced to the DGII, and the range is closed only when the
// DGII says it was annulled.
public class RangeAnnulmentTests
{
    [Fact]
    public async Task AnnulAsync_ForARangeWithATail_AnnouncesTheTailAndClosesTheRange()
    {
        await using var fixture = new AnnulmentFixture();
        await fixture.InitializeAsync();

        var result = await fixture.Service.AnnulAsync(fixture.RangeId, fixture.CompanyId);

        result.Should().BeOfType<Either<Error, RangeAnnulmentResult>.Right>();

        // The document that travels carries the tail of the range: the first
        // number it never used and the last one it owns.
        fixture.Dgii.Annulments.Should().Be(1);
        fixture.Dgii.LastAnnulment.Should().Contain("<RncEmisor>10100000101</RncEmisor>");
        fixture.Dgii.LastAnnulment.Should().Contain("<TipoeCF>31</TipoeCF>");
        fixture.Dgii.LastAnnulment.Should().Contain("<SecuenciaeNCFDesde>E310000000006</SecuenciaeNCFDesde>");
        fixture.Dgii.LastAnnulment.Should().Contain("<SecuenciaeNCFHasta>E310000000010</SecuenciaeNCFHasta>");
        fixture.Dgii.LastAnnulment.Should().Contain("<CantidadeNCFAnulados>5</CantidadeNCFAnulados>");

        var range = await fixture.RangeAsync();

        range.IsAnnulled.Should().BeTrue();
        range.AnnulledAt.Should().NotBeNull();
        range.CurrentSequence.Should().Be(range.To,
            "the annulment consumes the range: no number of it can be used again");
        range.AnnulmentResponse.Should().Contain("anuladas correctamente");
    }

    [Fact]
    public async Task AnnulAsync_ForARangeWithoutATail_FailsClosedAndTheRangeDoesNotChange()
    {
        await using var fixture = new AnnulmentFixture(currentSequence: 10);
        await fixture.InitializeAsync();

        var result = await fixture.Service.AnnulAsync(fixture.RangeId, fixture.CompanyId);

        result.Should().BeOfType<Either<Error, RangeAnnulmentResult>.Left>();
        ((Either<Error, RangeAnnulmentResult>.Left)result).Value.Message.Should().Contain("no numbers left");

        fixture.Dgii.Annulments.Should().Be(0, "there is nothing to announce");

        var range = await fixture.RangeAsync();
        range.IsAnnulled.Should().BeFalse();
        range.CurrentSequence.Should().Be(10);
    }

    [Fact]
    public async Task AnnulAsync_ForARangeThatWasAlreadyAnnulled_FailsClosed()
    {
        await using var fixture = new AnnulmentFixture();
        await fixture.InitializeAsync();

        await fixture.Service.AnnulAsync(fixture.RangeId, fixture.CompanyId);

        var second = await fixture.Service.AnnulAsync(fixture.RangeId, fixture.CompanyId);

        second.Should().BeOfType<Either<Error, RangeAnnulmentResult>.Left>();
        ((Either<Error, RangeAnnulmentResult>.Left)second).Value.Message.Should().Contain("already annulled");

        fixture.Dgii.Annulments.Should().Be(1, "a range is annulled once and only once");
    }

    [Fact]
    public async Task AnnulAsync_ForAnIssuerWithoutCertificate_FailsClosedAndTheRangeDoesNotChange()
    {
        await using var fixture = new AnnulmentFixture();
        await fixture.InitializeAsync();

        fixture.CertificateProvider
            .Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("There is no active signing certificate for the issuer RNC 10100000101."));

        var result = await fixture.Service.AnnulAsync(fixture.RangeId, fixture.CompanyId);

        result.Should().BeOfType<Either<Error, RangeAnnulmentResult>.Left>();
        ((Either<Error, RangeAnnulmentResult>.Left)result).Value.Message.Should().Contain("no active signing certificate");

        fixture.Dgii.Annulments.Should().Be(0);

        var range = await fixture.RangeAsync();
        range.IsAnnulled.Should().BeFalse();
        range.CurrentSequence.Should().Be(5);
    }

    [Fact]
    public async Task AnnulAsync_WhenTheDgiiRefuses_LeavesTheRangeAsItWasAndTheAnswerTravels()
    {
        await using var fixture = new AnnulmentFixture();
        await fixture.InitializeAsync();

        fixture.Dgii.Refuse();

        var result = await fixture.Service.AnnulAsync(fixture.RangeId, fixture.CompanyId);

        result.Should().BeOfType<Either<Error, RangeAnnulmentResult>.Left>();
        var error = ((Either<Error, RangeAnnulmentResult>.Left)result).Value;
        error.Message.Should().Contain("han sido utilizadas");

        var range = await fixture.RangeAsync();
        range.IsAnnulled.Should().BeFalse();
        range.CurrentSequence.Should().Be(5);
        range.AnnulmentResponse.Should().BeNull();
    }

    [Fact]
    public async Task AnnulAsync_ForARangeOfAnotherCompany_IsNotFound()
    {
        await using var fixture = new AnnulmentFixture();
        await fixture.InitializeAsync();

        var result = await fixture.Service.AnnulAsync(fixture.RangeId, fixture.OtherCompanyId);

        result.Should().BeOfType<Either<Error, RangeAnnulmentResult>.Left>();
        var error = ((Either<Error, RangeAnnulmentResult>.Left)result).Value;
        error.Kind.Should().Be(ErrorKind.NotFound);

        fixture.Dgii.Annulments.Should().Be(0);
    }

    private sealed class AnnulmentFixture : IAsyncDisposable
    {
        private const string IssuerRnc = "10100000101";
        private readonly SqliteConnection _connection;
        private readonly int _currentSequence;

        public AnnulmentFixture(int currentSequence = 5)
        {
            _currentSequence = currentSequence;
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            Options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(_connection)
                .Options;

            Context = new VolterpDbContext(Options);
        }

        public DbContextOptions<VolterpDbContext> Options { get; }
        public VolterpDbContext Context { get; }
        public DgiiStub Dgii { get; } = new();
        public Mock<ISigningCertificateProvider> CertificateProvider { get; } = new();
        public Guid CompanyId { get; private set; }
        public Guid OtherCompanyId { get; private set; }
        public Guid RangeId { get; private set; }
        public IRangeAnnulmentService Service { get; private set; } = null!;
        public UnitOfWork UnitOfWork { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await Context.Database.EnsureCreatedAsync();

            CompanyId = Guid.CreateVersion7();
            OtherCompanyId = Guid.CreateVersion7();

            Context.Companies.AddRange(
                Company(CompanyId, IssuerRnc),
                Company(OtherCompanyId, "10100000102"));

            RangeId = Guid.CreateVersion7();

            Context.ElectronicInvoiceSequences.Add(new ElectronicInvoiceSequence
            {
                Id = RangeId,
                CompanyId = CompanyId,
                IssuerTaxId = IssuerRnc,
                DocumentType = EcfDocumentType.FiscalCredit,
                From = 1,
                To = 10,
                ExpiresAt = new DateTime(2028, 12, 31),
                CurrentSequence = _currentSequence
            });

            await Context.SaveChangesAsync();

            CertificateProvider
                .Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateCertificate());

            var signer = new XmlDsigEcfSigner();
            var authenticator = new DgiiAuthenticator(Dgii, signer, new DgiiTokenCache());

            UnitOfWork = new UnitOfWork(Context);

            Service = new RangeAnnulmentService(
                UnitOfWork,
                signer,
                CertificateProvider.Object,
                authenticator,
                Dgii);
        }

        public async Task<ElectronicInvoiceSequence> RangeAsync()
        {
            Context.ChangeTracker.Clear();

            return await Context.ElectronicInvoiceSequences
                .IgnoreQueryFilters()
                .SingleAsync(range => range.Id == RangeId);
        }

        private static Company Company(Guid id, string taxId) => new()
        {
            Id = id,
            Name = "Distribuidora del Norte",
            LegalName = "Distribuidora del Norte SRL",
            TaxId = taxId,
            Address = "Calle Principal 1, Santo Domingo",
            Phone = "809-555-1234",
            Email = "facturacion@distribuidora.do"
        };

        private static X509Certificate2 CreateCertificate()
        {
            const string password = "volterp-test-password";

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
                certificate.Export(X509ContentType.Pfx, password),
                password,
                X509KeyStorageFlags.EphemeralKeySet);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    // The DGII as the tests see it: it answers the seed, the token and the
    // annulment of the ranges.
    private sealed class DgiiStub : IDgiiClient
    {
        public int Annulments { get; private set; }

        public string? LastAnnulment { get; private set; }

        private bool _refuse;

        public void Refuse() => _refuse = true;

        public Task<string> GetSeedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("<SemillaModel><valor>abc</valor><fecha>2019-03-13T14:33:32-04:00</fecha></SemillaModel>");

        public Task<DgiiToken> RequestTokenAsync(string signedSeedXml, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiToken("jwt", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow));

        public Task<DgiiReceipt> SendElectronicInvoiceAsync(string signedEcfXml, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiReceipt("track-1", null, null));

        public Task<DgiiStatus> GetStatusAsync(string trackId, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiStatus(
                trackId,
                DgiiVerdict.Accepted,
                "Aceptado",
                "10100000101",
                "E310000000001",
                true,
                DateTimeOffset.UtcNow,
                []));

        public Task<DgiiTrackLookup> GetTrackIdAsync(string issuerTaxId, string encf, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiTrackLookup("track-1", "Aceptado", DateTimeOffset.UtcNow));

        public Task<DgiiRangeAnnulmentAnswer> AnnulRangesAsync(string signedAnecfXml, string token, CancellationToken cancellationToken = default)
        {
            Annulments++;
            LastAnnulment = signedAnecfXml;

            var messages = _refuse
                ? new[] { "Las secuencias que está intentando anular han sido utilizadas" }
                : new[] { "Las secuencias fueron anuladas correctamente" };

            return Task.FromResult(new DgiiRangeAnnulmentAnswer(
                "10100000101",
                _refuse ? "2" : "1",
                _refuse ? "Anulacion rechazada" : "Anulacion realizada",
                messages)
            {
                RawBody = "{\"rnc\":\"10100000101\",\"codigo\":\"1\",\"nombre\":\"Anulacion realizada\",\"mensajes\":[\"Las secuencias fueron anuladas correctamente\"]}"
            });
        }
    }
}
