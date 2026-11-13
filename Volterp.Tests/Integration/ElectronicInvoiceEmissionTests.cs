using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EitherWay;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Volterp.Application.DTOs.ElectronicInvoiceDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Abstractions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.ElectronicInvoicing.Signing;
using Volterp.ElectronicInvoicing.Xml;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.UnitOfWork;
using Xunit;

namespace Volterp.Tests.Integration;

// The emission over the real stack: the repositories, the atomic sequence,
// the writer and the signer, with the DGII on the other side of the wire.
public class ElectronicInvoiceEmissionTests
{
    [Fact]
    public async Task IssueAsync_IssuesTheDocumentAndPersistsItWithItsTrackIdAndVerdict()
    {
        await using var fixture = new EmissionFixture();
        await fixture.InitializeAsync();

        var result = await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Right>();
        var dto = ((Either<Error, ElectronicInvoiceDto>.Right)result).Value;
        dto.ENCF.Should().Be("E320000000001");
        dto.Status.Should().Be(EcfStatus.Accepted);

        var invoice = await fixture.Context.ElectronicInvoices.SingleAsync();

        invoice.ENCF.Should().Be("E320000000001");
        invoice.CompanyId.Should().Be(fixture.CompanyId);
        invoice.SaleId.Should().Be(fixture.SaleId);
        invoice.DocumentType.Should().Be(EcfDocumentType.Consumer);
        invoice.IssuerTaxId.Should().Be("10100000101");
        invoice.Status.Should().Be(EcfStatus.Accepted);
        invoice.TrackId.Should().Be("track-1");
        invoice.SignedXml.Should().Contain("Signature");
        invoice.SignedAt.Should().NotBeNull();
        invoice.SentAt.Should().NotBeNull();
        invoice.Subtotal.Should().Be(200m);
        invoice.Itbis.Should().Be(36m);
        invoice.Total.Should().Be(236m);
    }

    [Fact]
    public async Task IssueAsync_TwiceForTwoSalesOfTheSameIssuer_TakesTwoConsecutiveNumbers()
    {
        await using var fixture = new EmissionFixture();
        await fixture.InitializeAsync();

        var first = await fixture.Service.IssueAsync(fixture.SaleId, fixture.CompanyId);
        var second = await fixture.Service.IssueAsync(fixture.SecondSaleId, fixture.CompanyId);

        ((Either<Error, ElectronicInvoiceDto>.Right)first).Value.ENCF.Should().Be("E320000000001");
        ((Either<Error, ElectronicInvoiceDto>.Right)second).Value.ENCF.Should().Be("E320000000002");

        var invoices = await fixture.Context.ElectronicInvoices.OrderBy(i => i.ENCF).ToListAsync();
        invoices.Should().HaveCount(2);
    }

    [Fact]
    public async Task IssueAsync_WithASaleOfAnotherCompany_DoesNotIssueIt()
    {
        await using var fixture = new EmissionFixture();
        await fixture.InitializeAsync();

        // The emission runs without an HTTP context, so the global tenant
        // filter is off and the sale of another company is readable: the
        // company travels by hand, and it is what stops the emission.
        var result = await fixture.Service.IssueAsync(fixture.OtherSaleId, fixture.CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Left>();
        var error = ((Either<Error, ElectronicInvoiceDto>.Left)result).Value;
        error.Message.Should().Contain("company");

        (await fixture.Context.ElectronicInvoices.CountAsync()).Should().Be(0);
        (await fixture.Context.ElectronicInvoiceSequences.CountAsync()).Should().Be(0,
            "a refused emission does not take a number from the issuer");
        fixture.Dgii.SendRequests.Should().Be(0);
    }

    private sealed class EmissionFixture : IAsyncDisposable
    {
        private const string IssuerRnc = "10100000101";

        private readonly SqliteConnection _connection;

        public EmissionFixture()
        {
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
        public Guid CompanyId { get; private set; }
        public Guid OtherCompanyId { get; private set; }
        public Guid SaleId { get; private set; }
        public Guid SecondSaleId { get; private set; }
        public Guid OtherSaleId { get; private set; }
        public IElectronicInvoiceService Service { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await Context.Database.EnsureCreatedAsync();

            CompanyId = Guid.CreateVersion7();
            OtherCompanyId = Guid.CreateVersion7();

            Context.Companies.AddRange(
                Company(CompanyId, IssuerRnc),
                Company(OtherCompanyId, "10100000102"));

            SaleId = Guid.CreateVersion7();
            SecondSaleId = Guid.CreateVersion7();
            OtherSaleId = Guid.CreateVersion7();

            Context.Sales.AddRange(
                Sale(SaleId, CompanyId, "V-0001"),
                Sale(SecondSaleId, CompanyId, "V-0002"),
                Sale(OtherSaleId, OtherCompanyId, "V-0003"));

            await Context.SaveChangesAsync();

            var certificates = new Mock<ISigningCertificateProvider>();
            certificates.Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateCertificate());

            var signer = new XmlDsigEcfSigner();
            var authenticator = new DgiiAuthenticator(Dgii, signer, new DgiiTokenCache());

            Service = new ElectronicInvoiceService(
                new UnitOfWork(Context),
                new EcfXmlWriter(),
                signer,
                certificates.Object,
                authenticator,
                Dgii);
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

        private static Sale Sale(Guid id, Guid companyId, string code)
        {
            var sale = new Sale
            {
                Id = id,
                CompanyId = companyId,
                Code = code,
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                CreatedAt = new DateTime(2026, 11, 4)
            };

            sale.AddItem(Guid.CreateVersion7(), "Café molido", 2, 100m, TaxCode.ItbisTasa1);

            return sale;
        }

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

    // The DGII as the tests see it: it answers the seed, the token, the
    // acknowledgement and the verdict.
    private sealed class DgiiStub : IDgiiClient
    {
        public int SendRequests { get; private set; }

        public Task<string> GetSeedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("<SemillaModel><valor>abc</valor><fecha>2019-03-13T14:33:32-04:00</fecha></SemillaModel>");

        public Task<DgiiToken> RequestTokenAsync(string signedSeedXml, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiToken("jwt", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow));

        public Task<DgiiReceipt> SendElectronicInvoiceAsync(string signedEcfXml, string token, CancellationToken cancellationToken = default)
        {
            SendRequests++;

            return Task.FromResult(new DgiiReceipt("track-1", null, null));
        }

        public Task<DgiiStatus> GetStatusAsync(string trackId, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiStatus(
                trackId,
                DgiiVerdict.Accepted,
                "Aceptado",
                "10100000101",
                "E320000000001",
                true,
                DateTimeOffset.UtcNow,
                []));
    }
}
