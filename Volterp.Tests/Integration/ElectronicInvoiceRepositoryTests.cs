using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Application.Abstractions;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;
using Volterp.Infrastructure.UnitOfWork;
using Xunit;

namespace Volterp.Tests.Integration;

public class ElectronicInvoiceRepositoryTests
{
    [Fact]
    public async Task GetBySaleIdAsync_ReturnsTheDocumentOfTheSale_WithItsStatusAndRejectionReason()
    {
        await using var fixture = new InvoiceFixture();
        await fixture.InitializeAsync();

        fixture.Context.ElectronicInvoices.Add(fixture.Invoice(
            EcfStatus.Rejected, "El RNC del receptor no existe."));

        await fixture.Context.SaveChangesAsync();

        var found = await fixture.Repository.GetBySaleIdAsync(fixture.SaleId);

        found.Should().NotBeNull();
        found!.ENCF.Should().Be("E310000000001");
        found.Status.Should().Be(EcfStatus.Rejected);
        found.VerdictNote.Should().Be("El RNC del receptor no existe.",
            "the emitter has to know why the DGII rejected the document");
    }

    [Fact]
    public async Task GetBySaleIdAsync_WithoutADocument_ReturnsNull()
    {
        await using var fixture = new InvoiceFixture();
        await fixture.InitializeAsync();

        var found = await fixture.Repository.GetBySaleIdAsync(fixture.SaleId);

        found.Should().BeNull("a sale without a document is the one that has to be issued");
    }

    [Fact]
    public async Task UnitOfWork_CommitsTheDocument_AndItIsReachableFromTheApplicationLayer()
    {
        await using var fixture = new InvoiceFixture();
        await fixture.InitializeAsync();

        var unitOfWork = new UnitOfWork(fixture.Context);

        await unitOfWork.ElectronicInvoices.AddAsync(fixture.Invoice(EcfStatus.Signed));
        await unitOfWork.CommitAsync();

        var found = await unitOfWork.ElectronicInvoices.GetByIdAsync(fixture.InvoiceId);

        found.Should().NotBeNull();
        found!.Status.Should().Be(EcfStatus.Signed);
        found.SignedXml.Should().Be("<ECF />");
    }

    [Fact]
    public async Task GetBySaleIdAsync_WithATenantContext_DoesNotReturnTheDocumentOfAnotherTenant()
    {
        await using var fixture = new InvoiceFixture();
        await fixture.InitializeAsync();

        fixture.Context.ElectronicInvoices.Add(fixture.Invoice(
            EcfStatus.Accepted,
            saleId: fixture.OtherSaleId,
            companyId: fixture.OtherCompanyId,
            encf: "E310000000002"));

        await fixture.Context.SaveChangesAsync();

        using var scopedContext = new VolterpDbContext(fixture.Options, new TestTenantContext(fixture.CompanyId));
        var scopedRepository = new ElectronicInvoiceRepository(scopedContext);

        var found = await scopedRepository.GetBySaleIdAsync(fixture.OtherSaleId);

        found.Should().BeNull("a tenant must never read the fiscal document of another tenant");
    }

    private sealed class TestTenantContext(Guid companyId) : ITenantContext
    {
        public Guid? CompanyId { get; } = companyId;

        public bool IsUnrestricted => false;
    }

    private sealed class InvoiceFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public InvoiceFixture()
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
        public Guid CompanyId { get; private set; }
        public Guid OtherCompanyId { get; private set; }
        public Guid SaleId { get; private set; }
        public Guid OtherSaleId { get; private set; }
        public Guid InvoiceId { get; private set; }
        public ElectronicInvoiceRepository Repository { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await Context.Database.EnsureCreatedAsync();

            CompanyId = Guid.CreateVersion7();
            OtherCompanyId = Guid.CreateVersion7();

            Context.Companies.AddRange(
                Company(CompanyId, "101500018"),
                Company(OtherCompanyId, "101500019"));

            SaleId = Guid.CreateVersion7();
            OtherSaleId = Guid.CreateVersion7();

            Context.Sales.AddRange(
                Sale(SaleId, CompanyId, "V-0001"),
                Sale(OtherSaleId, OtherCompanyId, "V-0002"));

            await Context.SaveChangesAsync();

            Repository = new ElectronicInvoiceRepository(Context);
        }

        public ElectronicInvoice Invoice(
            EcfStatus status,
            string? verdictNote = null,
            Guid? saleId = null,
            Guid? companyId = null,
            string encf = "E310000000001")
        {
            InvoiceId = Guid.CreateVersion7();

            return new ElectronicInvoice
            {
                Id = InvoiceId,
                CompanyId = companyId ?? CompanyId,
                SaleId = saleId ?? SaleId,
                ENCF = encf,
                DocumentType = EcfDocumentType.FiscalCredit,
                IssuerTaxId = "101500018",
                ReceiverTaxId = "210000001",
                Subtotal = 100m,
                Itbis = 18m,
                Total = 118m,
                SignedXml = "<ECF />",
                Status = status,
                VerdictNote = verdictNote
            };
        }

        private static Company Company(Guid id, string taxId)
            => new()
            {
                Id = id,
                Name = "Empresa",
                TaxId = taxId,
                Address = "Dirección",
                LegalName = "Empresa SRL",
                Phone = "8090000000",
                Email = "empresa@test.com"
            };

        private static Sale Sale(Guid id, Guid companyId, string code)
            => new()
            {
                Id = id,
                CompanyId = companyId,
                Code = code,
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.FiscalCredit,
                ReceptorTaxId = "210000001",
                Subtotal = 100m,
                Itbis = 18m,
                Total = 118m
            };

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
