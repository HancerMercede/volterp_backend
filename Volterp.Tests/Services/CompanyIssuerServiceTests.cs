using System.Linq.Expressions;
using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.CompanyIssuerDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Xunit;

namespace Volterp.Tests.Services;

// The issuer RNCs of the tenant: the branches that issue on its behalf. The
// RNC of the tenant itself is not registered here: it is the identity of the
// company.
public class CompanyIssuerServiceTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string CompanyRnc = "101500018";
    private const string BranchRnc = "101500026";

    [Fact]
    public async Task CreateAsync_StoresTheBranchWithTheCompanyOfTheCaller()
    {
        var (service, _, _) = BuildService();

        var result = await service.CreateAsync(Request(), CompanyId, UserId);

        result.Should().BeOfType<Either<Error, CompanyIssuerDto>.Right>();
        var issuer = ((Either<Error, CompanyIssuerDto>.Right)result).Value;
        issuer.CompanyId.Should().Be(CompanyId, "the company comes from the token, never from the body");
        issuer.TaxId.Should().Be(BranchRnc);
        issuer.LegalName.Should().Be("Sucursal Norte");
        issuer.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenTheRncIsNotValid_ReturnsLeft()
    {
        var (service, _, _) = BuildService();

        var result = await service.CreateAsync(Request() with { TaxId = "not-an-rnc" }, CompanyId, UserId);

        result.Should().BeOfType<Either<Error, CompanyIssuerDto>.Left>();
    }

    [Fact]
    public async Task CreateAsync_WhenTheRncIsTheOneOfTheCompany_ReturnsLeft()
    {
        var (service, _, _) = BuildService();

        var result = await service.CreateAsync(Request() with { TaxId = CompanyRnc }, CompanyId, UserId);

        result.Should().BeOfType<Either<Error, CompanyIssuerDto>.Left>(
            "the RNC of the company is already an issuer: it does not have to be registered");
    }

    [Fact]
    public async Task CreateAsync_WhenTheRncIsAlreadyRegistered_ReturnsLeft()
    {
        var (service, _, issuers) = BuildService();
        issuers.Setup(i => i.GetByCondictionsAsync(
                It.IsAny<Expression<Func<CompanyIssuer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyIssuer { CompanyId = CompanyId, TaxId = BranchRnc });

        var result = await service.CreateAsync(Request(), CompanyId, UserId);

        result.Should().BeOfType<Either<Error, CompanyIssuerDto>.Left>();
    }

    [Fact]
    public async Task CreateAsync_WithoutTheLegalName_ReturnsLeft()
    {
        var (service, _, _) = BuildService();

        var result = await service.CreateAsync(Request() with { LegalName = "  " }, CompanyId, UserId);

        result.Should().BeOfType<Either<Error, CompanyIssuerDto>.Left>();
    }

    [Fact]
    public async Task UpdateAsync_WhenTheBranchStopsIssuing_DeactivatesIt()
    {
        var (service, _, issuers) = BuildService();
        var issuer = new CompanyIssuer
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyId,
            TaxId = BranchRnc,
            LegalName = "Sucursal Norte",
            Status = EntityStatus.Active
        };

        issuers.Setup(i => i.GetByCondictionsAsync(
                It.IsAny<Expression<Func<CompanyIssuer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(issuer);

        var result = await service.UpdateAsync(
            issuer.Id, new UpdateCompanyIssuerRequest("Sucursal Norte", false), CompanyId, UserId);

        result.Should().BeOfType<Either<Error, CompanyIssuerDto>.Right>();
        issuer.IsActive.Should().BeFalse("a branch that does not issue cannot sign a document");
    }

    [Fact]
    public async Task UpdateAsync_OfAnotherCompany_ReturnsLeft()
    {
        var (service, _, issuers) = BuildService();
        issuers.Setup(i => i.GetByCondictionsAsync(
                It.IsAny<Expression<Func<CompanyIssuer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyIssuer?)null);

        var result = await service.UpdateAsync(
            Guid.NewGuid(), new UpdateCompanyIssuerRequest("Sucursal Norte", true), CompanyId, UserId);

        result.Should().BeOfType<Either<Error, CompanyIssuerDto>.Left>();
    }

    [Fact]
    public async Task GetAllAsync_FiltersByTheCompanyOfTheCaller()
    {
        var (service, _, issuers) = BuildService();
        Expression<Func<CompanyIssuer, bool>>? predicate = null;

        issuers.Setup(i => i.GetAllAsync(
                It.IsAny<Expression<Func<CompanyIssuer, bool>>>(), 1, 10,
                It.IsAny<CancellationToken>(),
                It.IsAny<Func<IQueryable<CompanyIssuer>, IQueryable<CompanyIssuer>>>()))
            .Callback<Expression<Func<CompanyIssuer, bool>>, int, int, CancellationToken,
                Func<IQueryable<CompanyIssuer>, IQueryable<CompanyIssuer>>>((p, _, _, _, _) => predicate = p)
            .ReturnsAsync(new PagedResult<CompanyIssuer> { Items = new List<CompanyIssuer>() });

        await service.GetAllAsync(CompanyId, 1, 10);

        predicate.Should().NotBeNull();
        var filter = predicate!.Compile();
        filter(new CompanyIssuer { CompanyId = CompanyId }).Should().BeTrue();
        filter(new CompanyIssuer { CompanyId = OtherCompanyId }).Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_OfAnotherCompany_ReturnsLeft()
    {
        var (service, _, issuers) = BuildService();
        issuers.Setup(i => i.GetByCondictionsAsync(
                It.IsAny<Expression<Func<CompanyIssuer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyIssuer?)null);

        var result = await service.GetByIdAsync(Guid.NewGuid(), CompanyId);

        result.Should().BeOfType<Either<Error, CompanyIssuerDto>.Left>();
    }

    private static CreateCompanyIssuerRequest Request()
        => new(BranchRnc, "Sucursal Norte");

    private static (CompanyIssuerService Service, Mock<IUnitOfWork> UnitOfWork, Mock<ICompanyIssuerRepository> Issuers) BuildService()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var companies = new Mock<ICompanyRepository>();
        var issuers = new Mock<ICompanyIssuerRepository>();

        companies.Setup(c => c.GetCompanyByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Company { Id = CompanyId, TaxId = CompanyRnc });

        unitOfWork.Setup(u => u.Companies).Returns(companies.Object);
        unitOfWork.Setup(u => u.CompanyIssuers).Returns(issuers.Object);
        unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return (new CompanyIssuerService(unitOfWork.Object), unitOfWork, issuers);
    }
}
