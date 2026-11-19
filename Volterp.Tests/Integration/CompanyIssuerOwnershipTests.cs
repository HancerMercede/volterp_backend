using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using EitherWay;
using Volterp.Application.DTOs.CompanyIssuerDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;
using Volterp.Infrastructure.UnitOfWork;
using Xunit;

namespace Volterp.Tests.Integration;

// An RNC belongs to one taxpayer. A tenant that takes the RNC of another
// one could issue in its name, so the registration refuses it and the
// resolution never hands it over.
public class CompanyIssuerOwnershipTests
{
    private const string CompanyARnc = "101500018";
    private const string CompanyBRnc = "101500026";
    private const string BranchRnc = "101500034";

    [Fact]
    public async Task CreateAsync_WithTheRncOfAnotherCompany_ReturnsLeft()
    {
        await using var fixture = new OwnershipFixture();
        await fixture.InitializeAsync();

        var result = await fixture.Service.CreateAsync(
            new CreateCompanyIssuerRequest(CompanyARnc, "Otra empresa"), fixture.CompanyBId, null);

        result.Should().BeOfType<Either<Error, CompanyIssuerDto>.Left>(
            "the RNC of another taxpayer cannot become an issuer of this one");
    }

    [Fact]
    public async Task CreateAsync_WithTheRncOfAnotherBranch_ReturnsLeft()
    {
        await using var fixture = new OwnershipFixture();
        await fixture.InitializeAsync();

        var result = await fixture.Service.CreateAsync(
            new CreateCompanyIssuerRequest(BranchRnc, "Sucursal ajena"), fixture.CompanyBId, null);

        result.Should().BeOfType<Either<Error, CompanyIssuerDto>.Left>(
            "an RNC already registered by another tenant is taken");
    }

    [Fact]
    public async Task ResolveIssuerTaxIdAsync_WithTheRncOfAnotherCompany_Fails()
    {
        await using var fixture = new OwnershipFixture();
        await fixture.InitializeAsync();

        var act = () => fixture.Repository.ResolveIssuerTaxIdAsync(fixture.CompanyBId, CompanyARnc);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "a tenant never issues with the RNC of another one");
    }

    [Fact]
    public async Task CreateAsync_WithTheRncOfItsOwnCompany_ReturnsLeft()
    {
        await using var fixture = new OwnershipFixture();
        await fixture.InitializeAsync();

        var result = await fixture.Service.CreateAsync(
            new CreateCompanyIssuerRequest(CompanyBRnc, "La propia"), fixture.CompanyBId, null);

        result.Should().BeOfType<Either<Error, CompanyIssuerDto>.Left>(
            "the RNC of the company is already an issuer: it does not register");
    }

    private sealed class OwnershipFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public OwnershipFixture()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(_connection)
                .Options;

            Context = new VolterpDbContext(options);
            UnitOfWork = new UnitOfWork(Context);
        }

        public VolterpDbContext Context { get; }

        public UnitOfWork UnitOfWork { get; }

        public Guid CompanyAId { get; private set; } = Guid.CreateVersion7();

        public Guid CompanyBId { get; private set; } = Guid.CreateVersion7();

        public CompanyIssuerRepository Repository { get; private set; } = null!;

        public CompanyIssuerService Service { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await Context.Database.EnsureCreatedAsync();

            Context.Companies.AddRange(
                Company(CompanyAId, CompanyARnc, "Empresa A"),
                Company(CompanyBId, CompanyBRnc, "Empresa B"));

            Context.CompanyIssuers.Add(new CompanyIssuer
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyAId,
                TaxId = BranchRnc,
                LegalName = "Sucursal de la empresa A",
                Status = EntityStatus.Active
            });

            await Context.SaveChangesAsync();

            Repository = new CompanyIssuerRepository(Context);
            Service = new CompanyIssuerService(UnitOfWork);
        }

        private static Company Company(Guid id, string taxId, string name)
            => new()
            {
                Id = id,
                Name = name,
                TaxId = taxId,
                Address = "Dirección",
                LegalName = name + " SRL",
                Phone = "8090000000",
                Email = "empresa@test.com"
            };

        public async ValueTask DisposeAsync()
        {
            UnitOfWork.Dispose();
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
