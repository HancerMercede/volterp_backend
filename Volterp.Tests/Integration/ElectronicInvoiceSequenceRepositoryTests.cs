using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Domain.Entities;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;
using Xunit;
using Volterp.ElectronicInvoicing;

namespace Volterp.Tests.Integration;

public class ElectronicInvoiceSequenceRepositoryTests
{
    [Fact]
    public async Task NextSequenceAsync_ReturnsANumberInsideTheRangeTheTaxpayerConfigured()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(EcfDocumentType.FiscalCredit, 500, 510, ExpiresOn());

        var first = await fixture.Repository.NextSequenceAsync(fixture.CompanyId, EcfDocumentType.FiscalCredit);
        var second = await fixture.Repository.NextSequenceAsync(fixture.CompanyId, EcfDocumentType.FiscalCredit);

        first.Should().Be(500);
        second.Should().Be(501);
        (await fixture.Context.ElectronicInvoiceSequences.AsNoTracking().SingleAsync()).CurrentSequence.Should().Be(501);
    }

    [Fact]
    public async Task NextSequenceAsync_NeverHandsOutANumberOutsideTheRange()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(EcfDocumentType.FiscalCredit, 100, 102, ExpiresOn());

        var numbers = new List<int>();
        for (var attempt = 0; attempt < 3; attempt++)
            numbers.Add(await fixture.Repository.NextSequenceAsync(fixture.CompanyId, EcfDocumentType.FiscalCredit));

        numbers.Should().Equal(100, 101, 102);
    }

    [Fact]
    public async Task NextSequenceAsync_WhenTheRangeRunsOut_FailsWithTheReason()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(EcfDocumentType.FiscalCredit, 10, 11, ExpiresOn());

        await fixture.Repository.NextSequenceAsync(fixture.CompanyId, EcfDocumentType.FiscalCredit);
        await fixture.Repository.NextSequenceAsync(fixture.CompanyId, EcfDocumentType.FiscalCredit);

        var act = () => fixture.Repository.NextSequenceAsync(fixture.CompanyId, EcfDocumentType.FiscalCredit);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*exhausted*");
        (await fixture.Context.ElectronicInvoiceSequences.AsNoTracking().SingleAsync()).CurrentSequence.Should().Be(11);
    }

    [Fact]
    public async Task NextSequenceAsync_WhenTheRangeExpired_FailsWithTheReason()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(EcfDocumentType.FiscalCredit, 1, 100, new DateTime(2020, 12, 31));

        var act = () => fixture.Repository.NextSequenceAsync(fixture.CompanyId, EcfDocumentType.FiscalCredit);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*expired*");
        (await fixture.Context.ElectronicInvoiceSequences.AsNoTracking().SingleAsync()).CurrentSequence.Should().Be(0);
    }

    [Fact]
    public async Task NextSequenceAsync_WhenTheTypeCarriesNoExpiry_NeverTreatsTheRangeAsExpired()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(EcfDocumentType.Consumer, 1, 100, new DateTime(2020, 12, 31));
        await fixture.AddRangeAsync(EcfDocumentType.CreditNote, 1, 100);

        var consumer = await fixture.Repository.NextSequenceAsync(fixture.CompanyId, EcfDocumentType.Consumer);
        var creditNote = await fixture.Repository.NextSequenceAsync(fixture.CompanyId, EcfDocumentType.CreditNote);

        consumer.Should().Be(1);
        creditNote.Should().Be(1);
    }

    [Fact]
    public async Task NextSequenceAsync_WithSeveralRanges_TakesTheOneThatStillHasRoom()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(EcfDocumentType.FiscalCredit, 1, 2, ExpiresOn(), position: 2);
        await fixture.AddRangeAsync(EcfDocumentType.FiscalCredit, 100, 200, ExpiresOn());

        var number = await fixture.Repository.NextSequenceAsync(fixture.CompanyId, EcfDocumentType.FiscalCredit);

        number.Should().Be(100);
    }

    [Fact]
    public async Task NextSequenceAsync_WithSeveralRanges_DoesNotSkipTheOneThatStillHasRoom()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(EcfDocumentType.FiscalCredit, 1, 200, ExpiresOn());
        await fixture.AddRangeAsync(EcfDocumentType.FiscalCredit, 300, 400, ExpiresOn());

        var number = await fixture.Repository.NextSequenceAsync(fixture.CompanyId, EcfDocumentType.FiscalCredit);

        number.Should().Be(1);
    }

    [Fact]
    public async Task NextSequenceAsync_WithSeveralIssuersInTheSameTenant_KeepsTheirRangesApart()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(EcfDocumentType.FiscalCredit, 1, 100, ExpiresOn(), issuerTaxId: "10100000101");
        await fixture.AddRangeAsync(EcfDocumentType.FiscalCredit, 500, 600, ExpiresOn(), issuerTaxId: "10100000102");

        var firstIssuer = await fixture.Repository.NextSequenceAsync(
            fixture.CompanyId, "10100000101", EcfDocumentType.FiscalCredit);
        var secondIssuer = await fixture.Repository.NextSequenceAsync(
            fixture.CompanyId, "10100000102", EcfDocumentType.FiscalCredit);

        firstIssuer.Should().Be(1);
        secondIssuer.Should().Be(500);
    }

    [Fact]
    public async Task NextSequenceAsync_WithoutARange_FailsWithTheReason()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();

        var act = () => fixture.Repository.NextSequenceAsync(fixture.CompanyId, EcfDocumentType.FiscalCredit);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*range*");
    }

    [Fact]
    public async Task NextSequenceAsync_WithARangeOfAnotherIssuer_KeepsTheRangesApart()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();
        await fixture.AddRangeAsync(EcfDocumentType.FiscalCredit, 1, 100, ExpiresOn(), issuerTaxId: "10100000999");

        var act = () => fixture.Repository.NextSequenceAsync(fixture.CompanyId, EcfDocumentType.FiscalCredit);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*range*");
    }

    [Fact]
    public async Task NextSequenceAsync_Concurrently_NeverHandsOutTheSameNumber()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"volterp-sequence-{Guid.NewGuid():N}");
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

                setup.ElectronicInvoiceSequences.Add(new ElectronicInvoiceSequence
                {
                    Id = Guid.CreateVersion7(),
                    CompanyId = companyId,
                    IssuerTaxId = issuerTaxId,
                    DocumentType = EcfDocumentType.FiscalCredit,
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
                    new ElectronicInvoiceSequenceRepository(first).NextSequenceAsync(companyId, EcfDocumentType.FiscalCredit),
                    new ElectronicInvoiceSequenceRepository(second).NextSequenceAsync(companyId, EcfDocumentType.FiscalCredit));

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

    private static VolterpDbContext CreateContext(string database)
    {
        return new VolterpDbContext(
            new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite($"Data Source={database};Pooling=False")
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
        public ElectronicInvoiceSequenceRepository Repository { get; private set; } = null!;

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

            Repository = new ElectronicInvoiceSequenceRepository(_context);
        }

        public async Task AddRangeAsync(
            EcfDocumentType documentType,
            int from,
            int to,
            DateTime? expiresAt = null,
            int? position = null,
            string? issuerTaxId = null)
        {
            _context.ElectronicInvoiceSequences.Add(new ElectronicInvoiceSequence
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

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
