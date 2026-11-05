using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Enums;
using Xunit;

namespace Volterp.Tests.Services;

/// <summary>
/// The dashboard service carries the two reporting filters from the controller
/// down to the repository, without interpreting them (feature dashboard-period-filters).
/// </summary>
public class DashboardServiceTests
{
    [Fact]
    public async Task GetDashboardDataAsync_PassesTheScopeAndTheChartWindowToTheRepository()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockRepository = new Mock<IDashboardRepository>();

        mockRepository
            .Setup(r => r.GetDashboardDataAsync(DashboardScope.Week, ChartWindow.Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildResponse());
        mockUnitOfWork.Setup(u => u.Dashboard).Returns(mockRepository.Object);

        var service = new DashboardService(mockUnitOfWork.Object);

        // ACT
        await service.GetDashboardDataAsync(DashboardScope.Week, ChartWindow.Year);

        // ASSERT
        mockRepository.Verify(
            r => r.GetDashboardDataAsync(DashboardScope.Week, ChartWindow.Year, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetDashboardDataAsync_ReturnsTheRepositoryResponse()
    {
        // ARRANGE
        var expected = BuildResponse();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockRepository = new Mock<IDashboardRepository>();

        mockRepository
            .Setup(r => r.GetDashboardDataAsync(It.IsAny<DashboardScope>(), It.IsAny<ChartWindow>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        mockUnitOfWork.Setup(u => u.Dashboard).Returns(mockRepository.Object);

        var service = new DashboardService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);

        // ASSERT
        result.Should().BeOfType<Either<Error, DashboardResponse>.Right>();
        var response = ((Either<Error, DashboardResponse>.Right)result).Value;
        response.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetDashboardDataAsync_ReturnsAnError_WhenTheRepositoryFails()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockRepository = new Mock<IDashboardRepository>();

        mockRepository
            .Setup(r => r.GetDashboardDataAsync(It.IsAny<DashboardScope>(), It.IsAny<ChartWindow>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the dashboard could not be read"));
        mockUnitOfWork.Setup(u => u.Dashboard).Returns(mockRepository.Object);

        var service = new DashboardService(mockUnitOfWork.Object);

        // ACT
        var result = await service.GetDashboardDataAsync(DashboardScope.Month, ChartWindow.Week);

        // ASSERT
        result.Should().BeOfType<Either<Error, DashboardResponse>.Left>();
        var error = ((Either<Error, DashboardResponse>.Left)result).Value;
        error.Message.Should().Be("the dashboard could not be read");
    }

    private static DashboardResponse BuildResponse() => new(
        Data: new DashboardDataDto(100m, 40m, 3, 60m, 10m, -5m, null, null),
        SalesChart: [new SalesChartDto("Lun", 10m, 5m)],
        Categories: [new CategoryDataDto("Bebidas", 100, "#FACC15", 100m)],
        TopProducts: [],
        Activities: [],
        Reminders: [],
        Stats: new DashboardStatsDto(1, 2, 3, 4));
}
