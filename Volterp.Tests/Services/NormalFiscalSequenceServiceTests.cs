using System.Linq.Expressions;
using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.NormalFiscalSequenceDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Xunit;

namespace Volterp.Tests.Services;

// The range the DGII authorized for the normal documents of the taxpayer:
// the same rules as the electronic one, plus the two the number of a normal
// document imposes (eight digits of sequence).
public class NormalFiscalSequenceServiceTests
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
        result.Should().BeOfType<Either<Error, NormalFiscalSequenceDto>.Right>();
        var range = ((Either<Error, NormalFiscalSequenceDto>.Right)result).Value;
        range.CompanyId.Should().Be(CompanyId, "the company comes from the token, never from the body");
        range.IssuerTaxId.Should().Be(IssuerRnc);
        range.DocumentType.Should().Be(NormalFiscalDocumentType.Consumer);
        range.From.Should().Be(1);
        range.To.Should().Be(10_000_000);
        range.CurrentSequence.Should().Be(1_200, "the taxpayer already used that many numbers on paper");
    }

    [Fact]
    public async Task CreateAsync_WhenTheRangeEndsBeforeItStarts_ReturnsLeft()
    {
        var (service, _) = BuildService();

        var result = await service.CreateAsync(Request() with { To = 0 }, CompanyId);

        result.Should().BeOfType<Either<Error, NormalFiscalSequenceDto>.Left>();
    }

    [Fact]
    public async Task CreateAsync_WhenTheRangeStartsBeforeOne_ReturnsLeft()
    {
        var (service, _) = BuildService();

        var result = await service.CreateAsync(Request() with { From = 0, Position = 0 }, CompanyId);

        result.Should().BeOfType<Either<Error, NormalFiscalSequenceDto>.Left>();
    }

    [Fact]
    public async Task CreateAsync_WhenTheRangeGoesBeyondTheEightDigitsOfTheNumber_ReturnsLeft()
    {
        var (service, _) = BuildService();

        // The number of a normal document carries eight digits of sequence: a
        // range beyond them would produce a number the column cannot hold.
        var result = await service.CreateAsync(
            Request() with { To = NormalFiscalSequence.MaxSequence + 1 }, CompanyId);

        result.Should().BeOfType<Either<Error, NormalFiscalSequenceDto>.Left>();
    }

    [Fact]
    public async Task CreateAsync_WhenThePositionIsOutsideTheRange_ReturnsLeft()
    {
        var (service, _) = BuildService();

        var result = await service.CreateAsync(Request() with { Position = 20_000_000 }, CompanyId);

        result.Should().BeOfType<Either<Error, NormalFiscalSequenceDto>.Left>();
    }

    [Fact]
    public async Task CreateAsync_WhenNothingHasBeenUsedYet_AcceptsThePositionBeforeTheRange()
    {
        var (service, _) = BuildService();

        // The taxpayer declares the number it is at. One before the start of
        // the range means that nothing was used yet, so the first number of
        // the range is still to be taken.
        var result = await service.CreateAsync(Request() with { From = 10, Position = 9 }, CompanyId);

        result.Should().BeOfType<Either<Error, NormalFiscalSequenceDto>.Right>();
    }

    [Fact]
    public async Task CreateAsync_WhenTheTypeCarriesAnExpiryAndThereIsNone_ReturnsLeft()
    {
        var (service, _) = BuildService();

        var result = await service.CreateAsync(
            Request() with { DocumentType = NormalFiscalDocumentType.FiscalCredit, ExpiresAt = null }, CompanyId);

        result.Should().BeOfType<Either<Error, NormalFiscalSequenceDto>.Left>();
    }

    [Fact]
    public async Task CreateAsync_WhenTheTypeDoesNotCarryAnExpiryAndOneIsSent_ReturnsLeft()
    {
        var (service, _) = BuildService();

        var result = await service.CreateAsync(
            Request() with { DocumentType = NormalFiscalDocumentType.Consumer, ExpiresAt = DateTime.UtcNow }, CompanyId);

        result.Should().BeOfType<Either<Error, NormalFiscalSequenceDto>.Left>();
    }

    [Fact]
    public async Task CreateAsync_WhenTheIssuerRncIsNotTheCompany_ReturnsLeft()
    {
        var (service, _) = BuildService();

        var result = await service.CreateAsync(Request() with { IssuerTaxId = "199999999" }, CompanyId);

        result.Should().BeOfType<Either<Error, NormalFiscalSequenceDto>.Left>();
    }

    [Fact]
    public async Task CreateAsync_WhenTheRangeIsAlreadyConfigured_ReturnsLeft()
    {
        var (service, unitOfWork) = BuildService();
        unitOfWork.Setup(u => u.NormalFiscalSequences.GetByCondictionsAsync(
                It.IsAny<Expression<Func<NormalFiscalSequence, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NormalFiscalSequence { CompanyId = CompanyId, IssuerTaxId = IssuerRnc });

        var result = await service.CreateAsync(Request(), CompanyId);

        result.Should().BeOfType<Either<Error, NormalFiscalSequenceDto>.Left>();
    }

    [Fact]
    public async Task UpdateAsync_WhenThePositionGoesBack_ReturnsLeft()
    {
        var (service, unitOfWork) = BuildService();
        unitOfWork.Setup(u => u.NormalFiscalSequences.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Configured());

        var result = await service.UpdateAsync(Guid.NewGuid(), Request() with { Position = 10 }, CompanyId);

        result.Should().BeOfType<Either<Error, NormalFiscalSequenceDto>.Left>(
            "a number already used on paper must never be handed out again");
    }

    [Fact]
    public async Task UpdateAsync_WhenThePositionGoesForward_MovesItAndPersists()
    {
        var (service, unitOfWork) = BuildService();
        var range = Configured();
        unitOfWork.Setup(u => u.NormalFiscalSequences.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(range);

        var result = await service.UpdateAsync(range.Id, Request() with { Position = 1_500 }, CompanyId);

        result.Should().BeOfType<Either<Error, NormalFiscalSequenceDto>.Right>();
        range.CurrentSequence.Should().Be(1_500);
        unitOfWork.Verify(
            u => u.NormalFiscalSequences.UpdateAsync(range, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenTheRangeGoesBeyondTheEightDigitsOfTheNumber_ReturnsLeft()
    {
        var (service, unitOfWork) = BuildService();
        unitOfWork.Setup(u => u.NormalFiscalSequences.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Configured());

        var result = await service.UpdateAsync(
            Guid.NewGuid(), Request() with { To = NormalFiscalSequence.MaxSequence + 1 }, CompanyId);

        result.Should().BeOfType<Either<Error, NormalFiscalSequenceDto>.Left>();
    }

    [Fact]
    public async Task GetAllAsync_FiltersByTheCompanyOfTheCaller()
    {
        var (service, unitOfWork) = BuildService();
        Expression<Func<NormalFiscalSequence, bool>>? predicate = null;

        unitOfWork.Setup(u => u.NormalFiscalSequences.GetAllAsync(
                It.IsAny<Expression<Func<NormalFiscalSequence, bool>>>(), 1, 10,
                It.IsAny<CancellationToken>(),
                It.IsAny<Func<IQueryable<NormalFiscalSequence>, IQueryable<NormalFiscalSequence>>>()))
            .Callback<Expression<Func<NormalFiscalSequence, bool>>, int, int, CancellationToken,
                Func<IQueryable<NormalFiscalSequence>, IQueryable<NormalFiscalSequence>>>((p, _, _, _, _) => predicate = p)
            .ReturnsAsync(new PagedResult<NormalFiscalSequence>
            {
                Items = new List<NormalFiscalSequence>()
            });

        await service.GetAllAsync(CompanyId, 1, 10);

        predicate.Should().NotBeNull();
        var filter = predicate!.Compile();
        filter(new NormalFiscalSequence { CompanyId = CompanyId }).Should().BeTrue();
        filter(new NormalFiscalSequence { CompanyId = OtherCompanyId }).Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_OfAnotherCompany_ReturnsLeft()
    {
        var (service, unitOfWork) = BuildService();
        unitOfWork.Setup(u => u.NormalFiscalSequences.GetByCondictionsAsync(
                It.IsAny<Expression<Func<NormalFiscalSequence, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NormalFiscalSequence?)null);

        var result = await service.GetByIdAsync(Guid.NewGuid(), CompanyId);

        result.Should().BeOfType<Either<Error, NormalFiscalSequenceDto>.Left>();
    }

    private static SaveNormalFiscalSequenceDto Request()
        => new(IssuerRnc, NormalFiscalDocumentType.Consumer, 1, 10_000_000, null, 1_200);

    private static NormalFiscalSequence Configured()
        => new()
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyId,
            IssuerTaxId = IssuerRnc,
            DocumentType = NormalFiscalDocumentType.Consumer,
            From = 1,
            To = 10_000_000,
            CurrentSequence = 1_200
        };

    private static (NormalFiscalSequenceService Service, Mock<IUnitOfWork> UnitOfWork) BuildService()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var companies = new Mock<ICompanyRepository>();
        var sequences = new Mock<INormalFiscalSequenceRepository>();

        companies.Setup(c => c.GetCompanyByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Company { Id = CompanyId, TaxId = IssuerRnc });

        unitOfWork.Setup(u => u.Companies).Returns(companies.Object);
        unitOfWork.Setup(u => u.NormalFiscalSequences).Returns(sequences.Object);

        return (new NormalFiscalSequenceService(unitOfWork.Object), unitOfWork);
    }
}
