using EitherWay;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Volterp.Api.Workers;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Xunit;

namespace Volterp.Tests.Services;

public class ElectronicInvoiceQueueWorkerTests
{
    private static readonly Guid FirstCompany = Guid.CreateVersion7();
    private static readonly Guid SecondCompany = Guid.CreateVersion7();

    [Fact]
    public async Task ProcessPendingAsync_ProcessesEveryCompanyWithPendingDocuments()
    {
        var invoices = new Mock<IElectronicInvoiceRepository>();
        invoices.Setup(i => i.GetCompanyIdsWithPendingEmissionAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([FirstCompany, SecondCompany]);

        var service = new Mock<IElectronicInvoiceService>();
        service.Setup(s => s.ProcessPendingAsync(FirstCompany, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Either<Error, int>.ToRight(1));
        service.Setup(s => s.ProcessPendingAsync(SecondCompany, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Either<Error, int>.ToRight(2));

        using var provider = Provider(invoices, service);
        var worker = Worker(provider);

        await worker.ProcessPendingAsync(CancellationToken.None);

        service.Verify(s => s.ProcessPendingAsync(FirstCompany, It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(s => s.ProcessPendingAsync(SecondCompany, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenOneCompanyFails_ContinuesWithTheNext()
    {
        var invoices = new Mock<IElectronicInvoiceRepository>();
        invoices.Setup(i => i.GetCompanyIdsWithPendingEmissionAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([FirstCompany, SecondCompany]);

        var service = new Mock<IElectronicInvoiceService>();
        service.Setup(s => s.ProcessPendingAsync(FirstCompany, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("The DGII did not answer."));
        service.Setup(s => s.ProcessPendingAsync(SecondCompany, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Either<Error, int>.ToRight(1));

        using var provider = Provider(invoices, service);
        var worker = Worker(provider);

        await worker.ProcessPendingAsync(CancellationToken.None);

        service.Verify(s => s.ProcessPendingAsync(SecondCompany, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ServiceProvider Provider(
        Mock<IElectronicInvoiceRepository> invoices,
        Mock<IElectronicInvoiceService> service)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.ElectronicInvoices).Returns(invoices.Object);

        var services = new ServiceCollection();
        services.AddScoped(_ => unitOfWork.Object);
        services.AddScoped(_ => service.Object);

        return services.BuildServiceProvider();
    }

    private static ElectronicInvoiceQueueWorker Worker(ServiceProvider provider)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Production");

        return new ElectronicInvoiceQueueWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            environment.Object,
            NullLogger<ElectronicInvoiceQueueWorker>.Instance);
    }
}
