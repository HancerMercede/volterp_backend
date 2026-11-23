using EitherWay;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
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

// The deletion of a sale: the fiscal trail of the DGII is not ours to
// erase, but a document that never reached the DGII is not a trail yet.
// The rule fails closed with a reason the caller can read, and the raw
// exception of the database never reaches the response.
public class DeleteSaleWithFiscalDocumentTests
{
    [Fact]
    public async Task ASaleWithoutADocumentIsDeleted()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();

        var result = await fixture.DeleteAsync();

        result.Should().BeOfType<Either<Error, Unit>.Right>();
        (await fixture.SaleCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ASaleWithADocumentThatNeverReachedTheDgiiIsDeletedWithItsDocument()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync(EcfStatus.Draft);

        var result = await fixture.DeleteAsync();

        result.Should().BeOfType<Either<Error, Unit>.Right>();
        (await fixture.SaleCountAsync()).Should().Be(0);
        (await fixture.DocumentCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ASaleWithASignedDocumentThatNeverReachedTheDgiiIsDeletedWithItsDocument()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync(EcfStatus.Signed);

        var result = await fixture.DeleteAsync();

        result.Should().BeOfType<Either<Error, Unit>.Right>();
        (await fixture.SaleCountAsync()).Should().Be(0);
        (await fixture.DocumentCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ASaleWithADocumentThatWasAlreadySentIsNotDeletedAndSaysWhy()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync(EcfStatus.Sent);

        var result = await fixture.DeleteAsync();

        result.Should().BeOfType<Either<Error, Unit>.Left>();
        ((Either<Error, Unit>.Left)result).Value.Message.Should().Contain("already sent");
        ((Either<Error, Unit>.Left)result).Value.Message.Should().NotContain("23503");
        ((Either<Error, Unit>.Left)result).Value.Message.Should().NotContain("FOREIGN KEY");

        (await fixture.SaleCountAsync()).Should().Be(1);
        (await fixture.DocumentCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ASaleWithAnAcceptedDocumentIsNotDeletedAndSaysWhy()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync(EcfStatus.Accepted);

        var result = await fixture.DeleteAsync();

        result.Should().BeOfType<Either<Error, Unit>.Left>();
        ((Either<Error, Unit>.Left)result).Value.Message.Should().Contain("already sent");

        (await fixture.SaleCountAsync()).Should().Be(1);
        (await fixture.DocumentCountAsync()).Should().Be(1);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<VolterpDbContext> _options;

        public Fixture()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<VolterpDbContext>().UseSqlite(_connection).Options;
            Context = new VolterpDbContext(_options);
        }

        public VolterpDbContext Context { get; }
        public UnitOfWork UnitOfWork { get; private set; } = null!;
        public SaleService Sales { get; private set; } = null!;
        public Mock<IElectronicInvoiceService> ElectronicIssuance { get; } = new();
        public Guid CompanyId { get; private set; }
        public Guid SaleId { get; private set; }
        public Guid ProductId { get; private set; }

        public async Task InitializeAsync(EcfStatus? documentStatus = null)
        {
            await Context.Database.EnsureCreatedAsync();

            CompanyId = Guid.CreateVersion7();
            SaleId = Guid.CreateVersion7();
            ProductId = Guid.CreateVersion7();

            Context.Companies.Add(new Company
            {
                Id = CompanyId,
                Name = "Distribuidora del Norte",
                LegalName = "Distribuidora del Norte SRL",
                TaxId = "10100000101",
                Address = "Calle Principal 1, Santo Domingo",
                Phone = "809-555-1234",
                Email = "facturacion@distribuidora.do",
                EmissionMode = CompanyEmissionMode.Normal
            });

            Context.Products.Add(new Product
            {
                Id = ProductId,
                CompanyId = CompanyId,
                Code = "P-CAFE",
                Name = "Café molido",
                Category = "Bebidas",
                Stock = 10,
                TaxCode = TaxCode.ItbisTasa1
            });

            var sale = new Sale
            {
                Id = SaleId,
                CompanyId = CompanyId,
                Code = "V-0001",
                Status = SaleStatus.Pending,
                DocumentType = EcfDocumentType.Consumer,
                CreatedAt = new DateTime(2026, 11, 16)
            };

            sale.AddItem(ProductId, "Café molido", 2, 100m, TaxCode.ItbisTasa1);

            Context.Sales.Add(sale);

            if (documentStatus is not null)
            {
                Context.ElectronicInvoices.Add(new ElectronicInvoice
                {
                    Id = Guid.CreateVersion7(),
                    CompanyId = CompanyId,
                    SaleId = SaleId,
                    ENCF = "B3100000000001",
                    DocumentType = EcfDocumentType.Consumer,
                    IssuerTaxId = "10100000101",
                    ReceiverTaxId = "101010101",
                    SignedXml = "<e-CF/>",
                    Status = documentStatus.Value
                });
            }

            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();

            UnitOfWork = new UnitOfWork(Context);
            Sales = new SaleService(
                UnitOfWork,
                ElectronicIssuance.Object,
                normalDocuments: new NormalFiscalDocumentService(UnitOfWork));
        }

        public Task<Either<Error, Unit>> DeleteAsync()
            => Sales.DeleteSaleAsync(SaleId, CompanyId);

        public async Task<int> SaleCountAsync()
            => await ReadAsync(context => context.Sales.AsNoTracking().CountAsync(sale => sale.Id == SaleId));

        public async Task<int> DocumentCountAsync()
            => await ReadAsync(context => context.ElectronicInvoices.AsNoTracking().CountAsync());

        public async Task<T> ReadAsync<T>(Func<VolterpDbContext, Task<T>> read)
        {
            await using var context = new VolterpDbContext(_options);
            return await read(context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
