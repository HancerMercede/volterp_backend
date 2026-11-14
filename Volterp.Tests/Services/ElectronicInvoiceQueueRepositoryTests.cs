using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Api.Http;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;
using Xunit;

namespace Volterp.Tests.Services;

public class ElectronicInvoiceQueueRepositoryTests
{
    [Fact]
    public async Task QueueQueries_WorkWithoutAnHttpTenantContext()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<VolterpDbContext>()
            .UseSqlite(connection)
            .Options;
        var companyId = Guid.CreateVersion7();
        var saleId = Guid.CreateVersion7();
        var invoiceId = Guid.CreateVersion7();
        var now = DateTime.UtcNow;

        await using (var seed = new VolterpDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Empresa",
                LegalName = "Empresa SRL",
                TaxId = "10100000101",
                Address = "Dirección",
                Phone = "8090000000",
                Email = "empresa@test.com"
            });
            seed.Sales.Add(new Sale
            {
                Id = saleId,
                CompanyId = companyId,
                Code = "V-0001",
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer
            });
            seed.ElectronicInvoices.Add(new ElectronicInvoice
            {
                Id = invoiceId,
                CompanyId = companyId,
                SaleId = saleId,
                ENCF = "E320000000001",
                DocumentType = EcfDocumentType.Consumer,
                IssuerTaxId = "10100000101",
                SignedXml = "<ECF />",
                Status = EcfStatus.Signed,
                CreatedAt = now.AddMinutes(-1)
            });
            await seed.SaveChangesAsync();
        }

        await using var workerContext = new VolterpDbContext(
            options,
            new HttpTenantContext(new HttpContextAccessor()));
        var repository = new ElectronicInvoiceRepository(workerContext);

        var companies = await repository.GetCompanyIdsWithPendingEmissionAsync(
            now,
            now.AddDays(-15),
            10);
        var pending = await repository.GetPendingForEmissionAsync(
            companyId,
            now,
            now.AddDays(-15),
            10);
        var claimed = await repository.TryClaimAsync(
            companyId,
            invoiceId,
            "worker-1",
            now.AddMinutes(5),
            now);
        var invoice = await repository.GetByCompanyAndIdAsync(companyId, invoiceId);

        companies.Should().ContainSingle().Which.Should().Be(companyId);
        pending.Should().ContainSingle().Which.Id.Should().Be(invoiceId);
        claimed.Should().BeTrue();
        invoice.Should().NotBeNull();
        invoice!.LeaseOwner.Should().Be("worker-1");
        invoice.AttemptCount.Should().Be(1);
    }
}
