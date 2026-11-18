using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EitherWay;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
using Volterp.ElectronicInvoicing.Xml;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;
using Xunit;

namespace Volterp.Tests.Integration;

// The risks that the fix of the verdict cycle did not close. They are
// preexisting and they are pinned here on purpose: the test says what happens
// today, so the day they are fixed the test says so.
//
// They are NOT failures of the suite: they are the known state of the code.
[Collection("VolterpApiFactory")]
public class T46OpenRiskVerifierTests
{
    private const string IssuerRnc = "10100000101";

    // OPEN RISK: the retrack is a last-write-wins over the whole document.
    // There is no token of concurrency, so a snapshot that was read before a
    // final verdict arrived writes its own state over it.
    [Fact]
    public async Task TheRetrackWritesItsSnapshotOverAVerdictThatArrivedMeanwhile()
    {
        var dgii = new DgiiStub { Verdict = DgiiVerdict.InProcess };
        var (factory, _) = Factory(dgii);
        using (factory)
        {
            var companyId = Guid.CreateVersion7();
            await SeedAsync(factory, companyId);

            // The verdict that arrives while the retrack is asking: the row
            // is written with the final state before the retrack writes the
            // snapshot it read.
            dgii.BeforeTheAnswer = async () =>
            {
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

                var invoice = await db.ElectronicInvoices
                    .IgnoreQueryFilters()
                    .SingleAsync(i => i.CompanyId == companyId);

                invoice.Status = EcfStatus.Accepted;
                invoice.NextCheckAt = null;
                await db.SaveChangesAsync();
            };

            using var scope = factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IElectronicInvoiceService>();
            await service.RetrackAsync(companyId);

            var invoice = await InvoiceAsync(factory, companyId);

            // OPEN RISK (preexisting): the final verdict is lost.
            invoice.Status.Should().Be(EcfStatus.InProcess,
                "OPEN RISK: the retrack writes the whole document it read, with no token of concurrency");
        }
    }

    // OPEN RISK: the number is taken in a statement of its own, before the
    // document exists. A failure of the persistence leaves the number
    // consumed with no document behind it.
    [Fact]
    public async Task AFailedPersistenceLeavesTheNumberConsumedWithoutADocument()
    {
        await using var fixture = new EmissionFixture();
        await fixture.InitializeAsync();

        // The number the next emission is going to take is already in the
        // table: the insert of the document cannot succeed.
        await fixture.AddDocumentAsync("E320000000001", Guid.CreateVersion7());

        var result = await Service(fixture.Context, theWinnerIsNotVisible: false)
            .IssueAsync(fixture.SaleId, fixture.CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Left>();

        // OPEN RISK (preexisting): the number is burned.
        (await fixture.CurrentSequenceAsync()).Should().Be(1,
            "OPEN RISK: the raw statement of the number is committed before the document is created");
        (await fixture.DocumentsOfTheSaleAsync()).Should().BeEmpty(
            "OPEN RISK: the number was consumed and no document carries it");
    }

    // OPEN RISK: the unique index keeps the sale from having two documents,
    // but the loser of the race has already taken another number.
    [Fact]
    public async Task TwoCompletionsOfTheSameSaleLeaveTheLoserWithANumberAndWithoutADocument()
    {
        await using var fixture = new EmissionFixture();
        await fixture.InitializeAsync(currentSequence: 1);

        // The winner of the race already has the document of the sale.
        await fixture.AddDocumentAsync("E320000000001", fixture.SaleId);

        // The loser does not see it yet: that is the window of the race.
        var result = await Service(fixture.Context, theWinnerIsNotVisible: true)
            .IssueAsync(fixture.SaleId, fixture.CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceDto>.Left>();

        // OPEN RISK (preexisting): the index saved the sale, the number did
        // not survive.
        (await fixture.CurrentSequenceAsync()).Should().Be(2,
            "OPEN RISK: the loser of the race consumed another number of the range");
        (await fixture.DocumentsOfTheSaleAsync()).Should().HaveCount(1,
            "the unique index keeps the sale from having two documents");
    }

    private static (ApiFactory Factory, DgiiStub Dgii) Factory(DgiiStub dgii)
    {
        var certificates = new Mock<ISigningCertificateProvider>();
        certificates.Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCertificate());

        var signer = new Mock<IEcfSigner>();
        signer.Setup(s => s.Sign(It.IsAny<string>(), It.IsAny<X509Certificate2>()))
            .Returns("<SemillaModel />");

        var factory = new ApiFactory(services =>
        {
            services.RemoveAll<IDgiiClient>();
            services.RemoveAll<ISigningCertificateProvider>();
            services.RemoveAll<IEcfSigner>();
            services.AddSingleton<IDgiiClient>(dgii);
            services.AddSingleton(certificates.Object);
            services.AddSingleton(signer.Object);
        });

        factory.EnsureSchemaCreated();
        return (factory, dgii);
    }

    private static async Task SeedAsync(ApiFactory factory, Guid companyId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
        var saleId = Guid.CreateVersion7();

        db.Companies.Add(new Company
        {
            Id = companyId,
            Name = $"Empresa {companyId}",
            LegalName = $"Empresa {companyId} SRL",
            TaxId = IssuerRnc,
            Address = "Calle Principal 1, Santo Domingo",
            Phone = "809-555-1234",
            Email = "facturacion@empresa.test"
        });
        db.Sales.Add(new Sale
        {
            Id = saleId,
            CompanyId = companyId,
            Code = "V-0001",
            Status = SaleStatus.Completed,
            DocumentType = EcfDocumentType.Consumer,
            Subtotal = 200m,
            Itbis = 36m,
            Total = 236m,
            CreatedAt = DateTime.UtcNow
        });
        db.ElectronicInvoices.Add(new ElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            SaleId = saleId,
            ENCF = "E320000000001",
            DocumentType = EcfDocumentType.Consumer,
            IssuerTaxId = IssuerRnc,
            SignedXml = "<ECF />",
            SignedAt = DateTime.UtcNow,
            TrackId = "track-1",
            SentAt = DateTime.UtcNow,
            Status = EcfStatus.Sent,
            NextCheckAt = DateTime.UtcNow.AddMinutes(-1)
        });

        await db.SaveChangesAsync();
    }

    private static async Task<ElectronicInvoice> InvoiceAsync(ApiFactory factory, Guid companyId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        return await db.ElectronicInvoices
            .IgnoreQueryFilters()
            .SingleAsync(invoice => invoice.CompanyId == companyId);
    }

    private static ElectronicInvoiceService Service(
        VolterpDbContext context, bool theWinnerIsNotVisible)
    {
        var real = new ElectronicInvoiceRepository(context);
        var invoices = new Mock<IElectronicInvoiceRepository>();

        if (theWinnerIsNotVisible)
        {
            // The window of the race: the document of the winner is not
            // visible to the loser.
            invoices.Setup(i => i.GetBySaleIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ElectronicInvoice?)null);
        }

        invoices.Setup(i => i.AddAsync(It.IsAny<ElectronicInvoice>(), It.IsAny<CancellationToken>()))
            .Returns((ElectronicInvoice document, CancellationToken _) =>
                real.AddAsync(document, CancellationToken.None));

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Sales).Returns(new SaleRepository(context));
        unitOfWork.Setup(u => u.Companies).Returns(new CompanyRepository(context));
        unitOfWork.Setup(u => u.ElectronicInvoiceSequences).Returns(new ElectronicInvoiceSequenceRepository(context));
        unitOfWork.Setup(u => u.CompanyIssuers).Returns(new CompanyIssuerRepository(context));
        unitOfWork.Setup(u => u.ElectronicInvoices).Returns(invoices.Object);
        unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => context.SaveChangesAsync(ct));

        var client = new Mock<IDgiiClient>();
        var signer = new Mock<IEcfSigner>();
        signer.Setup(s => s.Sign(It.IsAny<string>(), It.IsAny<X509Certificate2>()))
            .Returns(SignedXml);

        var certificates = new Mock<ISigningCertificateProvider>();
        certificates.Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCertificate());

        return new ElectronicInvoiceService(
            unitOfWork.Object,
            new EcfXmlWriter(),
            signer.Object,
            certificates.Object,
            new DgiiAuthenticator(client.Object, signer.Object, new DgiiTokenCache()),
            client.Object);
    }

    private const string SignedXml =
        "<ECF Signed=\"true\"><Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\">" +
        "<SignatureValue>BoXjC8ZmFrQnVuZXZlck9yaWdpbmFs</SignatureValue></Signature></ECF>";

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

    // The emission over a real database: the number, the range and the
    // document are the ones of the production code.
    private sealed class EmissionFixture : IAsyncDisposable
    {
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
        public Guid CompanyId { get; } = Guid.CreateVersion7();
        public Guid SaleId { get; } = Guid.CreateVersion7();

        public async Task InitializeAsync(int currentSequence = 0)
        {
            await Context.Database.EnsureCreatedAsync();

            Context.Companies.Add(new Company
            {
                Id = CompanyId,
                Name = "Empresa",
                LegalName = "Empresa SRL",
                TaxId = IssuerRnc,
                Address = "Calle Principal 1, Santo Domingo",
                Phone = "809-555-1234",
                Email = "facturacion@empresa.test"
            });

            Context.ElectronicInvoiceSequences.Add(new ElectronicInvoiceSequence
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyId,
                IssuerTaxId = IssuerRnc,
                DocumentType = EcfDocumentType.Consumer,
                From = 1,
                To = 1000,
                CurrentSequence = currentSequence
            });

            var sale = new Sale
            {
                Id = SaleId,
                CompanyId = CompanyId,
                Code = "V-0001",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                CreatedAt = DateTime.UtcNow
            };
            sale.AddItem(Guid.CreateVersion7(), "Café molido", 1, 100m, TaxCode.ItbisTasa1);
            Context.Sales.Add(sale);

            await Context.SaveChangesAsync();
        }

        public async Task AddDocumentAsync(string encf, Guid saleId)
        {
            var alreadyThere = await Context.Sales
                .IgnoreQueryFilters()
                .AnyAsync(sale => sale.Id == saleId);

            if (!alreadyThere)
            {
                Context.Sales.Add(new Sale
                {
                    Id = saleId,
                    CompanyId = CompanyId,
                    Code = $"V-{encf}",
                    Status = SaleStatus.Completed,
                    DocumentType = EcfDocumentType.Consumer,
                    Subtotal = 100m,
                    Itbis = 18m,
                    Total = 118m
                });
            }

            Context.ElectronicInvoices.Add(new ElectronicInvoice
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyId,
                SaleId = saleId,
                ENCF = encf,
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = IssuerRnc,
                SignedXml = "<ECF />",
                Status = EcfStatus.Accepted
            });

            await Context.SaveChangesAsync();
        }

        public async Task<int> CurrentSequenceAsync()
        {
            // The number is taken with a raw statement, so the row has to be
            // read again: the tracked one is the one before the statement.
            var sequence = await Context.ElectronicInvoiceSequences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(s => s.CompanyId == CompanyId);

            return sequence.CurrentSequence;
        }

        public async Task<List<ElectronicInvoice>> DocumentsOfTheSaleAsync()
        {
            return await Context.ElectronicInvoices
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(i => i.SaleId == SaleId)
                .ToListAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class DgiiStub : IDgiiClient
    {
        public DgiiVerdict Verdict { get; set; } = DgiiVerdict.Accepted;

        // What happens in the middle of the conversation with the DGII.
        public Func<Task>? BeforeTheAnswer { get; set; }

        public Task<string> GetSeedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("<SemillaModel><valor>abc</valor><fecha>2019-03-13T14:33:32-04:00</fecha></SemillaModel>");

        public Task<DgiiToken> RequestTokenAsync(string signedSeedXml, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiToken("jwt", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow));

        public Task<DgiiReceipt> SendElectronicInvoiceAsync(string signedEcfXml, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiReceipt("track-1", null, null));

        public async Task<DgiiStatus> GetStatusAsync(string trackId, string token, CancellationToken cancellationToken = default)
        {
            if (BeforeTheAnswer is not null)
                await BeforeTheAnswer();

            return new DgiiStatus(
                trackId,
                Verdict,
                Verdict.ToString(),
                IssuerRnc,
                "E320000000001",
                true,
                DateTimeOffset.UtcNow,
                []);
        }

        public Task<DgiiTrackLookup> GetTrackIdAsync(string issuerTaxId, string encf, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiTrackLookup("track-1", "Aceptado", DateTimeOffset.UtcNow));
    }
}
