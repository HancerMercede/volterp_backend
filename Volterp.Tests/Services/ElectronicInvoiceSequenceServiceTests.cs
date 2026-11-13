using System.Linq.Expressions;
using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.ElectronicInvoiceSequenceDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;
using Xunit;

namespace Volterp.Tests.Services;

public class ElectronicInvoiceSequenceServiceTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();
    private const string IssuerRnc = "101500018";

    [Fact]
    public async Task CreateAsync_StoresTheRangeWithTheCompanyOfTheCaller()
    {
        // ARRANGE
        var (service, unitOfWork) = BuildService();
        unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // ACT
        var result = await service.CreateAsync(Request(), CompanyId);

        // ASSERT
        result.Should().BeOfType<Either<Error, ElectronicInvoiceSequenceDto>.Right>();
        var range = ((Either<Error, ElectronicInvoiceSequenceDto>.Right)result).Value;
        range.CompanyId.Should().Be(CompanyId, "the company comes from the token, never from the body");
        range.IssuerTaxId.Should().Be(IssuerRnc);
        range.From.Should().Be(1);
        range.To.Should().Be(10_000_000);
        range.CurrentSequence.Should().Be(1_200, "the taxpayer already used that many numbers on paper");
    }

    [Fact]
    public async Task CreateAsync_WhenTheRangeEndsBeforeItStarts_ReturnsLeft()
    {
        var (service, _) = BuildService();

        var result = await service.CreateAsync(Request() with { To = 0 }, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceSequenceDto>.Left>();
    }

    [Fact]
    public async Task CreateAsync_WhenThePositionIsOutsideTheRange_ReturnsLeft()
    {
        var (service, _) = BuildService();

        var result = await service.CreateAsync(Request() with { Position = 20_000_000 }, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceSequenceDto>.Left>();
    }

    [Fact]
    public async Task CreateAsync_WhenTheTypeCarriesAnExpiryAndThereIsNone_ReturnsLeft()
    {
        var (service, _) = BuildService();

        var result = await service.CreateAsync(
            Request() with { DocumentType = EcfDocumentType.FiscalCredit, ExpiresAt = null }, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceSequenceDto>.Left>();
    }

    [Fact]
    public async Task CreateAsync_WhenTheTypeDoesNotCarryAnExpiryAndOneIsSent_ReturnsLeft()
    {
        var (service, _) = BuildService();

        var result = await service.CreateAsync(
            Request() with { DocumentType = EcfDocumentType.Consumer, ExpiresAt = DateTime.UtcNow }, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceSequenceDto>.Left>();
    }

    [Fact]
    public async Task CreateAsync_WhenTheIssuerRncIsNotTheCompany_ReturnsLeft()
    {
        var (service, _) = BuildService();

        var result = await service.CreateAsync(Request() with { IssuerTaxId = "199999999" }, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceSequenceDto>.Left>();
    }

    [Fact]
    public async Task CreateAsync_WhenTheRangeIsAlreadyConfigured_ReturnsLeft()
    {
        var (service, unitOfWork) = BuildService();
        unitOfWork.Setup(u => u.ElectronicInvoiceSequences.GetByCondictionsAsync(
                It.IsAny<Expression<Func<ElectronicInvoiceSequence, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ElectronicInvoiceSequence { CompanyId = CompanyId, IssuerTaxId = IssuerRnc });

        var result = await service.CreateAsync(Request(), CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceSequenceDto>.Left>();
    }

    [Fact]
    public async Task UpdateAsync_WhenThePositionGoesBack_ReturnsLeft()
    {
        var (service, unitOfWork) = BuildService();
        unitOfWork.Setup(u => u.ElectronicInvoiceSequences.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Configured());

        var result = await service.UpdateAsync(Guid.NewGuid(), Request() with { Position = 10 }, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceSequenceDto>.Left>(
            "a number already used on paper must never be handed out again");
    }

    [Fact]
    public async Task UpdateAsync_WhenThePositionGoesForward_MovesItAndPersists()
    {
        var (service, unitOfWork) = BuildService();
        var range = Configured();
        unitOfWork.Setup(u => u.ElectronicInvoiceSequences.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(range);

        var result = await service.UpdateAsync(range.Id, Request() with { Position = 1_500 }, CompanyId);

        result.Should().BeOfType<Either<Error, ElectronicInvoiceSequenceDto>.Right>();
        range.CurrentSequence.Should().Be(1_500);
        unitOfWork.Verify(
            u => u.ElectronicInvoiceSequences.UpdateAsync(range, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByTheCompanyOfTheCaller()
    {
        var (service, unitOfWork) = BuildService();
        Expression<Func<ElectronicInvoiceSequence, bool>>? predicate = null;

        unitOfWork.Setup(u => u.ElectronicInvoiceSequences.GetAllAsync(
                It.IsAny<Expression<Func<ElectronicInvoiceSequence, bool>>>(), 1, 10,
                It.IsAny<CancellationToken>(),
                It.IsAny<Func<IQueryable<ElectronicInvoiceSequence>, IQueryable<ElectronicInvoiceSequence>>>()))
            .Callback<Expression<Func<ElectronicInvoiceSequence, bool>>, int, int, CancellationToken,
                Func<IQueryable<ElectronicInvoiceSequence>, IQueryable<ElectronicInvoiceSequence>>>((p, _, _, _, _) => predicate = p)
            .ReturnsAsync(new PagedResult<ElectronicInvoiceSequence>
            {
                Items = new List<ElectronicInvoiceSequence>()
            });

        await service.GetAllAsync(CompanyId, 1, 10);

        predicate.Should().NotBeNull();
        var filter = predicate!.Compile();
        filter(new ElectronicInvoiceSequence { CompanyId = CompanyId }).Should().BeTrue();
        filter(new ElectronicInvoiceSequence { CompanyId = OtherCompanyId }).Should().BeFalse();
    }

    private static SaveElectronicInvoiceSequenceDto Request()
        => new(IssuerRnc, EcfDocumentType.Consumer, 1, 10_000_000, null, 1_200);

    private static ElectronicInvoiceSequence Configured()
        => new()
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyId,
            IssuerTaxId = IssuerRnc,
            DocumentType = EcfDocumentType.Consumer,
            From = 1,
            To = 10_000_000,
            CurrentSequence = 1_200
        };

    private static (ElectronicInvoiceSequenceService Service, Mock<IUnitOfWork> UnitOfWork) BuildService()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var companies = new Mock<ICompanyRepository>();
        var sequences = new Mock<IElectronicInvoiceSequenceRepository>();

        companies.Setup(c => c.GetCompanyByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Company { Id = CompanyId, TaxId = IssuerRnc });

        unitOfWork.Setup(u => u.Companies).Returns(companies.Object);
        unitOfWork.Setup(u => u.ElectronicInvoiceSequences).Returns(sequences.Object);

        return (new ElectronicInvoiceSequenceService(unitOfWork.Object), unitOfWork);
    }
}
