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

// The lines of a sale are replaced, not merged. The sale arrives without
// tracking, so the lines that are gone are invisible to Entity Framework:
// they survive with the same SaleId and every update leaves its own lines
// behind. The sale grew with every update and the stock came back once per
// historic row.
public class SaleItemReplacementTests
{
    [Fact]
    public async Task UpdateSaleAsync_LeavesOnlyTheLinesOfTheNewSale()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();

        var result = await fixture.UpdateAsync(fixture.BreadId, quantity: 3, unitPrice: 50m);

        result.Should().BeOfType<Either<Error, SaleDto>.Right>();

        var lines = await fixture.ReadAsync(context => context.SaleItems
            .AsNoTracking()
            .Where(item => item.SaleId == fixture.SaleId)
            .ToListAsync());

        lines.Should().HaveCount(1);
        lines[0].ProductId.Should().Be(fixture.BreadId);
        lines[0].Quantity.Should().Be(3);
    }

    [Fact]
    public async Task UpdateSaleAsync_Twice_DoesNotAccumulateLines()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();

        await fixture.UpdateAsync(fixture.BreadId, quantity: 3, unitPrice: 50m);
        var result = await fixture.UpdateAsync(fixture.BreadId, quantity: 4, unitPrice: 50m);

        // When the update does not go through, the reason says why: a test
        // that fails without a reason costs a whole build to diagnose.
        result.Should().BeOfType<Either<Error, SaleDto>.Right>(
            because: result is Either<Error, SaleDto>.Left failure
                ? $"the update said: {failure.Value.Message}"
                : "the update went through");

        var lines = await fixture.ReadAsync(context => context.SaleItems
            .AsNoTracking()
            .Where(item => item.SaleId == fixture.SaleId)
            .ToListAsync());

        lines.Should().HaveCount(1);
        lines[0].Quantity.Should().Be(4);
    }

    [Fact]
    public async Task UpdateSaleAsync_Twice_DoesNotInflateTheStock()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();

        await fixture.UpdateAsync(fixture.BreadId, quantity: 3, unitPrice: 50m);
        await fixture.UpdateAsync(fixture.BreadId, quantity: 4, unitPrice: 50m);

        // The sale reserves four breads: the stock of the breads is the one
        // the reservation left. A sale that kept its old lines would give
        // the stock back once per historic row.
        var bread = await fixture.ReadAsync(context => context.Products
            .AsNoTracking()
            .SingleAsync(product => product.Id == fixture.BreadId));

        bread.Stock.Should().Be(Fixture.InitialStock - 4);
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

        public const int InitialStock = 50;

        public VolterpDbContext Context { get; }
        public UnitOfWork UnitOfWork { get; private set; } = null!;
        public SaleService Sales { get; private set; } = null!;
        public Mock<IElectronicInvoiceService> ElectronicIssuance { get; } = new();
        public Guid CompanyId { get; private set; }
        public Guid SaleId { get; private set; }
        public Guid CoffeeId { get; private set; }
        public Guid BreadId { get; private set; }

        public async Task InitializeAsync()
        {
            await Context.Database.EnsureCreatedAsync();

            CompanyId = Guid.CreateVersion7();
            SaleId = Guid.CreateVersion7();
            CoffeeId = Guid.CreateVersion7();
            BreadId = Guid.CreateVersion7();

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

            // A created sale reserves its stock: the lines it carries are
            // already taken out of the inventory. The seeded sale has to keep
            // that invariant, or the stock the test expects (the one the
            // reservation left) is not the stock the system would have.
            Context.Products.Add(new Product
            {
                Id = CoffeeId,
                CompanyId = CompanyId,
                Code = "P-CAFE",
                Name = "Café molido",
                Category = "Bebidas",
                Stock = InitialStock - 2,
                TaxCode = TaxCode.ItbisTasa1
            });

            Context.Products.Add(new Product
            {
                Id = BreadId,
                CompanyId = CompanyId,
                Code = "P-PAN",
                Name = "Pan integral",
                Category = "Panadería",
                Stock = InitialStock - 1,
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

            sale.AddItem(CoffeeId, "Café molido", 2, 100m, TaxCode.ItbisTasa1);
            sale.AddItem(BreadId, "Pan integral", 1, 50m, TaxCode.ItbisTasa1);

            Context.Sales.Add(sale);

            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();

            UnitOfWork = new UnitOfWork(Context);
            Sales = new SaleService(
                UnitOfWork,
                ElectronicIssuance.Object,
                normalDocuments: new NormalFiscalDocumentService(UnitOfWork));
        }

        public Task<Either<Error, SaleDto>> UpdateAsync(Guid productId, int quantity, decimal unitPrice)
            => Sales.UpdateSaleAsync(
                SaleId,
                CompanyId,
                new UpdateSaleRequest(
                    ClienteId: null,
                    ClienteName: "Cliente de Prueba SRL",
                    Status: SaleStatus.Pending,
                    Total: quantity * unitPrice,
                    Notes: null,
                    Items:
                    [
                        new CreateSaleItemRequest(
                            productId,
                            "Producto",
                            null,
                            null,
                            null,
                            quantity,
                            unitPrice,
                            quantity * unitPrice)
                    ]),
                Guid.CreateVersion7());

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
