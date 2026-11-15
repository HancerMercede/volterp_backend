using EitherWay;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Volterp.Application.DTOs.NormalFiscalDocumentDtos;
using Volterp.Application.DTOs.SaleDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.UnitOfWork;
using Xunit;

namespace Volterp.Tests.Integration;

// The paper path over the real stack: the sale, the authorized range of the
// normal documents, the atomic allocation and the record of the document. The
// number of the paper document comes from the range the DGII authorized for
// the normal documents, and the number, the document and the completion of
// the sale land together.
public class NormalFiscalDocumentRepositoryTests
{
    [Fact]
    public async Task CompletingAPaperSale_RecordsTheNormalDocumentAndLeavesNoElectronicInvoice()
    {
        await using var fixture = new PaperFixture();
        await fixture.InitializeAsync();

        var result = await fixture.CompleteSaleAsync();

        result.Should().BeOfType<Either<Error, SaleDto>.Right>();

        var sale = await fixture.ReadAsync(context => context.Sales
            .AsNoTracking()
            .SingleAsync(s => s.Id == fixture.SaleId));
        sale.Status.Should().Be(SaleStatus.Completed);

        var document = await fixture.ReadAsync(context => context.NormalFiscalDocuments
            .AsNoTracking()
            .SingleAsync());

        document.Number.Should().Be("B0200000001");
        document.Number.Should().HaveLength(11);
        document.DocumentType.Should().Be(NormalFiscalDocumentType.Consumer);
        document.CompanyId.Should().Be(fixture.CompanyId);
        document.SaleId.Should().Be(fixture.SaleId);
        document.IssuerTaxId.Should().Be(fixture.IssuerTaxId);
        document.ReceiverTaxId.Should().Be("101010101");
        document.ReceiverName.Should().Be("Cliente de Prueba SRL");
        document.Subtotal.Should().Be(200m);
        document.Itbis.Should().Be(36m);
        document.Total.Should().Be(236m);
        document.IssuedAt.Should().NotBe(default);

        (await fixture.ReadAsync(context => context.ElectronicInvoices.CountAsync())).Should().Be(0);
        fixture.ElectronicIssuance.Verify(e => e.IssueAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheNormalPath_ConsumesOnlyTheRangeOfTheNormalDocuments()
    {
        await using var fixture = new PaperFixture();
        await fixture.InitializeAsync();

        await fixture.CompleteSaleAsync();

        var normal = await fixture.ReadAsync(context => context.NormalFiscalSequences
            .AsNoTracking()
            .SingleAsync());
        normal.CurrentSequence.Should().Be(1);

        var electronic = await fixture.ReadAsync(context => context.ElectronicInvoiceSequences
            .AsNoTracking()
            .SingleAsync());
        electronic.CurrentSequence.Should().Be(0,
            "the normal document never consumes the range of the electronic ones");
    }

    [Fact]
    public async Task TheNormalAndTheElectronicRanges_AreIndependent()
    {
        await using var fixture = new PaperFixture();
        await fixture.InitializeAsync();

        await fixture.CompleteSaleAsync();

        // The electronic path takes a number of its own range: the two spaces
        // of numbers do not overlap.
        var electronic = await fixture.UnitOfWork.ElectronicInvoiceSequences
            .NextSequenceAsync(fixture.CompanyId, EcfDocumentType.Consumer);

        electronic.Should().Be(1);

        var document = await fixture.ReadAsync(context => context.NormalFiscalDocuments
            .AsNoTracking()
            .SingleAsync());
        document.Number.Should().Be("B0200000001");
        document.Number.Should().NotStartWith("E");
    }

    [Fact]
    public async Task WithoutARange_TheSaleDoesNotCompleteAndNoNumberIsTaken()
    {
        await using var fixture = new PaperFixture();
        await fixture.InitializeAsync(withRange: false);

        var result = await fixture.CompleteSaleAsync();

        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        ((Either<Error, SaleDto>.Left)result).Value.Message.Should().Contain("range");

        (await fixture.ReadAsync(context => context.Sales
            .AsNoTracking()
            .SingleAsync(s => s.Id == fixture.SaleId))).Status.Should().Be(SaleStatus.Pending);

        (await fixture.ReadAsync(context => context.NormalFiscalDocuments.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task WithAnExpiredRange_TheSaleDoesNotCompleteAndNoNumberIsTaken()
    {
        await using var fixture = new PaperFixture();
        await fixture.InitializeAsync(
            documentType: NormalFiscalDocumentType.FiscalCredit,
            expiresOn: new DateTime(2020, 12, 31));

        var result = await fixture.CompleteSaleAsync();

        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        ((Either<Error, SaleDto>.Left)result).Value.Message.Should().Contain("expired");

        (await fixture.ReadAsync(context => context.Sales
            .AsNoTracking()
            .SingleAsync(s => s.Id == fixture.SaleId))).Status.Should().Be(SaleStatus.Pending);

        (await fixture.ReadAsync(context => context.NormalFiscalDocuments.CountAsync())).Should().Be(0);

        var range = await fixture.ReadAsync(context => context.NormalFiscalSequences
            .AsNoTracking()
            .SingleAsync());
        range.CurrentSequence.Should().Be(0);
    }

    [Fact]
    public async Task WithAnExhaustedRange_TheSaleDoesNotCompleteAndNoNumberIsTaken()
    {
        await using var fixture = new PaperFixture();
        await fixture.InitializeAsync(from: 1, to: 1, position: 1);

        var result = await fixture.CompleteSaleAsync();

        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        ((Either<Error, SaleDto>.Left)result).Value.Message.Should().Contain("exhausted");

        (await fixture.ReadAsync(context => context.Sales
            .AsNoTracking()
            .SingleAsync(s => s.Id == fixture.SaleId))).Status.Should().Be(SaleStatus.Pending);

        (await fixture.ReadAsync(context => context.NormalFiscalDocuments.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task WhenTheDocumentCannotBePersisted_TheNumberIsNotBurned()
    {
        await using var fixture = new PaperFixture(new FailingDocumentStore());
        await fixture.InitializeAsync();

        var result = await fixture.CompleteSaleAsync();

        result.Should().BeOfType<Either<Error, SaleDto>.Left>();

        // A number without a document is a fiscal hole: the transaction that
        // took the number takes back the number with the document.
        (await fixture.ReadAsync(context => context.Sales
            .AsNoTracking()
            .SingleAsync(s => s.Id == fixture.SaleId))).Status.Should().Be(SaleStatus.Pending);

        (await fixture.ReadAsync(context => context.NormalFiscalDocuments.CountAsync())).Should().Be(0);

        var range = await fixture.ReadAsync(context => context.NormalFiscalSequences
            .AsNoTracking()
            .SingleAsync());
        range.CurrentSequence.Should().Be(0);
    }

    [Fact]
    public async Task CompletingTheSameSaleTwice_DoesNotTakeAnotherNumber()
    {
        await using var fixture = new PaperFixture();
        await fixture.InitializeAsync();

        await fixture.CompleteSaleAsync();
        var second = await fixture.CompleteSaleAsync();

        second.Should().BeOfType<Either<Error, SaleDto>.Left>();

        (await fixture.ReadAsync(context => context.NormalFiscalDocuments.CountAsync())).Should().Be(1);

        var range = await fixture.ReadAsync(context => context.NormalFiscalSequences
            .AsNoTracking()
            .SingleAsync());
        range.CurrentSequence.Should().Be(1);
    }

    [Fact]
    public async Task TheDocumentOfTheSaleIsKept_WithoutTakingAnotherNumber()
    {
        await using var fixture = new PaperFixture();
        await fixture.InitializeAsync();

        await fixture.CompleteSaleAsync();

        var service = new NormalFiscalDocumentService(fixture.UnitOfWork);
        var again = await service.IssueAsync(fixture.SaleId, fixture.CompanyId);

        again.Should().BeOfType<Either<Error, NormalFiscalDocumentDto>.Right>();
        ((Either<Error, NormalFiscalDocumentDto>.Right)again).Value.Number.Should().Be("B0200000001");

        (await fixture.ReadAsync(context => context.NormalFiscalDocuments.CountAsync())).Should().Be(1);

        var range = await fixture.ReadAsync(context => context.NormalFiscalSequences
            .AsNoTracking()
            .SingleAsync());
        range.CurrentSequence.Should().Be(1);
    }

    [Fact]
    public async Task AnotherCompany_CannotIssueTheDocumentOfASaleOfAnotherCompany()
    {
        await using var fixture = new PaperFixture();
        await fixture.InitializeAsync();

        var service = new NormalFiscalDocumentService(fixture.UnitOfWork);

        var result = await service.IssueAsync(fixture.SaleId, fixture.OtherCompanyId);

        result.Should().BeOfType<Either<Error, NormalFiscalDocumentDto>.Left>();
        ((Either<Error, NormalFiscalDocumentDto>.Left)result).Value.Message.Should().Contain("another company");

        (await fixture.ReadAsync(context => context.NormalFiscalDocuments.CountAsync())).Should().Be(0);

        var range = await fixture.ReadAsync(context => context.NormalFiscalSequences
            .AsNoTracking()
            .SingleAsync());
        range.CurrentSequence.Should().Be(0);
    }

    [Fact]
    public async Task AnotherCompany_DoesNotSeeTheDocumentOfTheFirstOne()
    {
        await using var fixture = new PaperFixture();
        await fixture.InitializeAsync();

        await fixture.CompleteSaleAsync();

        var seen = await fixture.UnitOfWork.NormalFiscalDocuments
            .GetBySaleIdAsync(fixture.OtherCompanyId, fixture.SaleId);

        seen.Should().BeNull();
    }

    [Fact]
    public async Task TheNumberOfAnIssuer_IsUniqueAcrossItsDocuments()
    {
        await using var fixture = new PaperFixture();
        await fixture.InitializeAsync();

        await fixture.CompleteSaleAsync();

        var second = new NormalFiscalDocument
        {
            Id = Guid.CreateVersion7(),
            CompanyId = fixture.CompanyId,
            SaleId = Guid.CreateVersion7(),
            Number = "B0200000001",
            DocumentType = NormalFiscalDocumentType.Consumer,
            IssuerTaxId = fixture.IssuerTaxId,
            Total = 100m
        };

        fixture.Context.NormalFiscalDocuments.Add(second);

        var act = async () => await fixture.Context.SaveChangesAsync();

        // The same number twice for the same issuer is a fiscal hole: the
        // database is the one that refuses it.
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task TheDatabaseRefusesTwoDocumentsForTheSameSale()
    {
        await using var fixture = new PaperFixture();
        await fixture.InitializeAsync();

        await fixture.CompleteSaleAsync();

        var second = new NormalFiscalDocument
        {
            Id = Guid.CreateVersion7(),
            CompanyId = fixture.CompanyId,
            SaleId = fixture.SaleId,
            Number = "B0200000002",
            DocumentType = NormalFiscalDocumentType.Consumer,
            IssuerTaxId = fixture.IssuerTaxId,
            Total = 100m
        };

        fixture.Context.NormalFiscalDocuments.Add(second);

        var act = async () => await fixture.Context.SaveChangesAsync();

        // One sale, one document: two of them would burn two fiscal numbers
        // for the same sale.
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private sealed class PaperFixture : IAsyncDisposable
    {
        private const string IssuerRnc = "10100000101";

        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<VolterpDbContext> _options;

        public PaperFixture(SaveChangesInterceptor? interceptor = null)
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var builder = new DbContextOptionsBuilder<VolterpDbContext>().UseSqlite(_connection);

            if (interceptor is not null)
                builder.AddInterceptors(interceptor);

            _options = builder.Options;
            Context = new VolterpDbContext(_options);
        }

        public VolterpDbContext Context { get; }
        public UnitOfWork UnitOfWork { get; private set; } = null!;
        public SaleService Sales { get; private set; } = null!;
        public Mock<IElectronicInvoiceService> ElectronicIssuance { get; } = new();
        public Guid CompanyId { get; private set; }
        public Guid OtherCompanyId { get; private set; }
        public Guid SaleId { get; private set; }
        public Sale Sale { get; private set; } = null!;
        public string IssuerTaxId => IssuerRnc;

        public async Task InitializeAsync(
            bool withRange = true,
            NormalFiscalDocumentType documentType = NormalFiscalDocumentType.Consumer,
            DateTime? expiresOn = null,
            int from = 1,
            int to = 50_000_000,
            int position = 0)
        {
            await Context.Database.EnsureCreatedAsync();

            CompanyId = Guid.CreateVersion7();
            OtherCompanyId = Guid.CreateVersion7();
            SaleId = Guid.CreateVersion7();

            Context.Companies.AddRange(
                Company(CompanyId, IssuerRnc),
                Company(OtherCompanyId, "10100000102"));

            Sale = SaleOf(SaleId, CompanyId, documentType);
            Context.Sales.Add(Sale);

            if (withRange)
            {
                Context.NormalFiscalSequences.Add(new NormalFiscalSequence
                {
                    Id = Guid.CreateVersion7(),
                    CompanyId = CompanyId,
                    IssuerTaxId = IssuerRnc,
                    DocumentType = documentType,
                    From = from,
                    To = to,
                    ExpiresAt = expiresOn,
                    CurrentSequence = position
                });
            }

            // The range of the electronic documents is a range of its own: the
            // paper document never touches it.
            Context.ElectronicInvoiceSequences.Add(new ElectronicInvoiceSequence
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyId,
                IssuerTaxId = IssuerRnc,
                DocumentType = EcfDocumentType.Consumer,
                From = 1,
                To = 50_000_000,
                CurrentSequence = 0
            });

            await Context.SaveChangesAsync();

            // The sale is read again by the service, the way a request reads
            // it: nothing stays tracked.
            Context.ChangeTracker.Clear();

            UnitOfWork = new UnitOfWork(Context);
            Sales = new SaleService(
                UnitOfWork,
                ElectronicIssuance.Object,
                normalDocuments: new NormalFiscalDocumentService(UnitOfWork));
        }

        public Task<Either<Error, SaleDto>> CompleteSaleAsync()
            => Sales.CompleteSaleAsync(SaleId, CompanyId, Guid.CreateVersion7());

        // The truth is in the database: the raw statement that takes the
        // number does not go through the change tracker.
        public async Task<T> ReadAsync<T>(Func<VolterpDbContext, Task<T>> read)
        {
            await using var context = new VolterpDbContext(_options);
            return await read(context);
        }

        private static Company Company(Guid id, string taxId) => new()
        {
            Id = id,
            Name = "Distribuidora del Norte",
            LegalName = "Distribuidora del Norte SRL",
            TaxId = taxId,
            Address = "Calle Principal 1, Santo Domingo",
            Phone = "809-555-1234",
            Email = "facturacion@distribuidora.do",
            EmissionMode = CompanyEmissionMode.Paper
        };

        private static Sale SaleOf(Guid id, Guid companyId, NormalFiscalDocumentType documentType)
        {
            var sale = new Sale
            {
                Id = id,
                CompanyId = companyId,
                Code = "V-0001",
                Status = SaleStatus.Pending,
                DocumentType = documentType switch
                {
                    NormalFiscalDocumentType.FiscalCredit => EcfDocumentType.FiscalCredit,
                    NormalFiscalDocumentType.DebitNote => EcfDocumentType.DebitNote,
                    NormalFiscalDocumentType.CreditNote => EcfDocumentType.CreditNote,
                    _ => EcfDocumentType.Consumer
                },
                ReceptorTaxId = "101010101",
                ClienteName = "Cliente de Prueba SRL",
                CreatedAt = new DateTime(2026, 11, 15)
            };

            sale.AddItem(Guid.CreateVersion7(), "Café molido", 2, 100m, TaxCode.ItbisTasa1);

            return sale;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    // The store that refuses the document: the number is taken and the
    // document cannot be written.
    private sealed class FailingDocumentStore : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            RefuseTheDocument(eventData);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            RefuseTheDocument(eventData);
            return new ValueTask<InterceptionResult<int>>(result);
        }

        private static void RefuseTheDocument(DbContextEventData eventData)
        {
            var added = eventData.Context?.ChangeTracker
                .Entries<NormalFiscalDocument>()
                .Any(entry => entry.State == EntityState.Added) == true;

            if (added)
                throw new InvalidOperationException("The document could not be persisted.");
        }
    }
}
