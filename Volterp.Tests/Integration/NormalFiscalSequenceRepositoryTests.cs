using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;
using Xunit;

namespace Volterp.Tests.Integration;

// The range the DGII authorized for the normal (paper) documents of an
// issuer. It is a range of its own: the electronic one is a different table
// and the two never share a position. The number is only taken inside the
// transaction that persists the document that carries it.
public class NormalFiscalSequenceRepositoryTests
{
    [Fact]
    public async Task NextSequenceAsync_ReturnsANumberInsideTheRangeTheTaxpayerConfigured()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(NormalFiscalDocumentType.Consumer, 500, 510, ExpiresOn());

        var first = await fixture.NextAsync(NormalFiscalDocumentType.Consumer);
        var second = await fixture.NextAsync(NormalFiscalDocumentType.Consumer);

        first.Should().Be(500);
        second.Should().Be(501);
        (await fixture.Context.NormalFiscalSequences.AsNoTracking().SingleAsync()).CurrentSequence.Should().Be(501);
    }

    [Fact]
    public async Task NextSequenceAsync_NeverHandsOutANumberOutsideTheRange()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(NormalFiscalDocumentType.Consumer, 100, 102, ExpiresOn());

        var numbers = new List<int>();
        for (var attempt = 0; attempt < 3; attempt++)
            numbers.Add(await fixture.NextAsync(NormalFiscalDocumentType.Consumer));

        numbers.Should().Equal(100, 101, 102);
    }

    [Fact]
    public async Task NextSequenceAsync_WithoutATransaction_FailsClosedAndTakesNoNumber()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(NormalFiscalDocumentType.Consumer, 1, 100, ExpiresOn());

        var act = () => fixture.Repository.NextSequenceAsync(
            fixture.CompanyId, fixture.IssuerTaxId, NormalFiscalDocumentType.Consumer);

        // A number that leaves the transaction burns a number with no document
        // behind it.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*transaction*");
        (await fixture.Context.NormalFiscalSequences.AsNoTracking().SingleAsync()).CurrentSequence.Should().Be(0);
    }

    [Fact]
    public async Task NextSequenceAsync_WhenTheRangeRunsOut_FailsWithTheReason()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(NormalFiscalDocumentType.Consumer, 10, 11, ExpiresOn());

        await fixture.NextAsync(NormalFiscalDocumentType.Consumer);
        await fixture.NextAsync(NormalFiscalDocumentType.Consumer);

        var act = () => fixture.NextAsync(NormalFiscalDocumentType.Consumer);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*exhausted*");
        (await fixture.Context.NormalFiscalSequences.AsNoTracking().SingleAsync()).CurrentSequence.Should().Be(11);
    }

    [Fact]
    public async Task NextSequenceAsync_WhenTheRangeExpired_FailsWithTheReason()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(
            NormalFiscalDocumentType.FiscalCredit, 1, 100, new DateTime(2020, 12, 31));

        var act = () => fixture.NextAsync(NormalFiscalDocumentType.FiscalCredit);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*expired*");
        (await fixture.Context.NormalFiscalSequences.AsNoTracking().SingleAsync()).CurrentSequence.Should().Be(0);
    }

    [Fact]
    public async Task NextSequenceAsync_WhenTheTypeCarriesNoExpiry_NeverTreatsTheRangeAsExpired()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(
            NormalFiscalDocumentType.Consumer, 1, 100, new DateTime(2020, 12, 31));
        await fixture.AddRangeAsync(NormalFiscalDocumentType.CreditNote, 1, 100);

        var consumer = await fixture.NextAsync(NormalFiscalDocumentType.Consumer);
        var creditNote = await fixture.NextAsync(NormalFiscalDocumentType.CreditNote);

        consumer.Should().Be(1);
        creditNote.Should().Be(1);
    }

    [Fact]
    public async Task NextSequenceAsync_WithSeveralRanges_TakesTheOneThatStillHasRoom()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(NormalFiscalDocumentType.Consumer, 1, 2, ExpiresOn(), position: 2);
        await fixture.AddRangeAsync(NormalFiscalDocumentType.Consumer, 100, 200, ExpiresOn());

        var number = await fixture.NextAsync(NormalFiscalDocumentType.Consumer);

        number.Should().Be(100);
    }

    [Fact]
    public async Task NextSequenceAsync_WithSeveralIssuersInTheSameTenant_KeepsTheirRangesApart()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(
            NormalFiscalDocumentType.Consumer, 1, 100, ExpiresOn(), issuerTaxId: "10100000101");
        await fixture.AddRangeAsync(
            NormalFiscalDocumentType.Consumer, 500, 600, ExpiresOn(), issuerTaxId: "10100000102");

        var firstIssuer = await fixture.NextAsync(NormalFiscalDocumentType.Consumer, "10100000101");
        var secondIssuer = await fixture.NextAsync(NormalFiscalDocumentType.Consumer, "10100000102");

        firstIssuer.Should().Be(1);
        secondIssuer.Should().Be(500);
    }

    [Fact]
    public async Task NextSequenceAsync_WithoutARange_FailsWithTheReason()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();

        var act = () => fixture.NextAsync(NormalFiscalDocumentType.Consumer);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*range*");
    }

    [Fact]
    public async Task NextSequenceAsync_WithARangeOfAnotherIssuer_KeepsTheRangesApart()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(
            NormalFiscalDocumentType.Consumer, 1, 100, ExpiresOn(), issuerTaxId: "10100000999");

        var act = () => fixture.NextAsync(NormalFiscalDocumentType.Consumer);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*range*");
    }

    [Fact]
    public async Task NextSequenceAsync_NeverTouchesTheElectronicRange()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(NormalFiscalDocumentType.Consumer, 1, 100, ExpiresOn());
        await fixture.AddElectronicRangeAsync(1, 100, ExpiresOn());

        await fixture.NextAsync(NormalFiscalDocumentType.Consumer);

        (await fixture.Context.NormalFiscalSequences.AsNoTracking().SingleAsync()).CurrentSequence.Should().Be(1);
        (await fixture.Context.ElectronicInvoiceSequences.AsNoTracking().SingleAsync()).CurrentSequence.Should().Be(0,
            "the normal document never consumes the range of the electronic ones");
    }

    [Fact]
    public async Task NextSequenceAsync_WithTheHighestPosition_FitsTheNumberOfTheDocument()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(
            NormalFiscalDocumentType.Consumer,
            NormalFiscalSequence.MaxSequence,
            NormalFiscalSequence.MaxSequence,
            ExpiresOn(),
            position: NormalFiscalSequence.MaxSequence - 1);

        var number = await fixture.NextAsync(NormalFiscalDocumentType.Consumer);

        number.Should().Be(NormalFiscalSequence.MaxSequence);
        NormalFiscalDocument.BuildNumber(NormalFiscalDocumentType.Consumer, number).Should().HaveLength(11);
    }

    [Fact]
    public async Task NextSequenceAsync_WithARangeBeyondTheHighestPosition_FailsClosed()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(
            NormalFiscalDocumentType.Consumer,
            1,
            NormalFiscalSequence.MaxSequence + 1,
            ExpiresOn());

        var act = () => fixture.NextAsync(NormalFiscalDocumentType.Consumer);

        // The number of a normal document carries eight digits: a range that
        // goes beyond them cannot be used.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*maximum sequence*");
        (await fixture.Context.NormalFiscalSequences.AsNoTracking().SingleAsync()).CurrentSequence.Should().Be(0);
    }

    [Fact]
    public async Task NextSequenceAsync_Concurrently_NeverHandsOutTheSameNumber()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"volterp-normal-sequence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var database = Path.Combine(directory, "volterp.db");
            var companyId = Guid.CreateVersion7();
            const string issuerTaxId = "10100000101";

            await using (var setup = CreateContext(database))
            {
                await setup.Database.EnsureCreatedAsync();

                setup.Companies.Add(new Company
                {
                    Id = companyId,
                    Name = "Empresa",
                    TaxId = issuerTaxId,
                    Address = "Dirección",
                    LegalName = "Empresa SRL",
                    Phone = "8090000000",
                    Email = "empresa@test.com"
                });

                setup.NormalFiscalSequences.Add(new NormalFiscalSequence
                {
                    Id = Guid.CreateVersion7(),
                    CompanyId = companyId,
                    IssuerTaxId = issuerTaxId,
                    DocumentType = NormalFiscalDocumentType.Consumer,
                    From = 1,
                    To = 100,
                    ExpiresAt = ExpiresOn(),
                    CurrentSequence = 0
                });

                await setup.SaveChangesAsync();
            }

            await using (var first = CreateContext(database))
            await using (var second = CreateContext(database))
            {
                var allocations = await Task.WhenAll(
                    AllocateAsync(first, companyId, issuerTaxId),
                    AllocateAsync(second, companyId, issuerTaxId));

                allocations.Should().OnlyHaveUniqueItems();
                allocations.Should().BeEquivalentTo(new[] { 1, 2 });
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static DateTime ExpiresOn() => new(2028, 12, 31);

    // Each allocation carries its own transaction, the way the service that
    // persists the document takes the number.
    private static async Task<int> AllocateAsync(
        VolterpDbContext context, Guid companyId, string issuerTaxId)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var number = await new NormalFiscalSequenceRepository(context).NextSequenceAsync(
            companyId, issuerTaxId, NormalFiscalDocumentType.Consumer);

        await transaction.CommitAsync();

        return number;
    }

    private static VolterpDbContext CreateContext(string database)
    {
        // The two concurrent allocations wait for the lock of the file instead
        // of failing on it.
        return new VolterpDbContext(
            new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite($"Data Source={database};Pooling=False;Default Timeout=30")
                .Options);
    }

    private sealed class SequenceFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly VolterpDbContext _context;

        public SequenceFixture()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new VolterpDbContext(options);
        }

        public Guid CompanyId { get; private set; }
        public string IssuerTaxId { get; private set; } = string.Empty;
        public VolterpDbContext Context => _context;
        public NormalFiscalSequenceRepository Repository { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await _context.Database.EnsureCreatedAsync();

            CompanyId = Guid.CreateVersion7();
            IssuerTaxId = CompanyId.ToString("N")[^9..];

            _context.Companies.Add(new Company
            {
                Id = CompanyId,
                Name = "Empresa",
                TaxId = IssuerTaxId,
                Address = "Dirección",
                LegalName = "Empresa SRL",
                Phone = "8090000000",
                Email = "empresa@test.com"
            });

            await _context.SaveChangesAsync();

            Repository = new NormalFiscalSequenceRepository(_context);
        }

        // The number is taken inside a transaction, the way the service that
        // persists the document takes it.
        public async Task<int> NextAsync(
            NormalFiscalDocumentType documentType, string? issuerTaxId = null)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var number = await Repository.NextSequenceAsync(
                CompanyId, issuerTaxId ?? IssuerTaxId, documentType);

            await transaction.CommitAsync();

            return number;
        }

        public async Task AddRangeAsync(
            NormalFiscalDocumentType documentType,
            int from,
            int to,
            DateTime? expiresAt = null,
            int? position = null,
            string? issuerTaxId = null)
        {
            _context.NormalFiscalSequences.Add(new NormalFiscalSequence
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyId,
                IssuerTaxId = issuerTaxId ?? IssuerTaxId,
                DocumentType = documentType,
                From = from,
                To = to,
                ExpiresAt = expiresAt,
                CurrentSequence = position ?? from - 1
            });

            await _context.SaveChangesAsync();
        }

        public async Task AddElectronicRangeAsync(int from, int to, DateTime? expiresAt)
        {
            _context.ElectronicInvoiceSequences.Add(new ElectronicInvoiceSequence
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyId,
                IssuerTaxId = IssuerTaxId,
                DocumentType = EcfDocumentType.Consumer,
                From = from,
                To = to,
                ExpiresAt = expiresAt,
                CurrentSequence = 0
            });

            await _context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
