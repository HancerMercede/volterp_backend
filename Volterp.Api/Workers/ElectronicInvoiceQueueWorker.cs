using EitherWay;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Policies;

namespace Volterp.Api.Workers;

public sealed class ElectronicInvoiceQueueWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IHostEnvironment hostEnvironment,
    ILogger<ElectronicInvoiceQueueWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private const int CompanyBatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (hostEnvironment.IsEnvironment("Testing"))
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The electronic invoice queue could not be processed.");
            }

            try
            {
                await Task.Delay(PollInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task ProcessPendingAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var service = scope.ServiceProvider.GetRequiredService<IElectronicInvoiceService>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var cutoff = new ContingencyWindow().QueueCutoff(now);

        var companyIds = await unitOfWork.ElectronicInvoices.GetCompanyIdsWithPendingEmissionAsync(
            now,
            cutoff,
            CompanyBatchSize,
            ct);

        foreach (var companyId in companyIds)
        {
            try
            {
                var result = await service.ProcessPendingAsync(companyId, ct);

                if (result is Either<Error, int>.Left failure)
                {
                    logger.LogError(
                        "The electronic invoice queue of company {CompanyId} failed: {Error}",
                        companyId,
                        failure.Value.Message);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "The electronic invoice queue of company {CompanyId} failed.",
                    companyId);
            }

            try
            {
                var result = await service.RetrackAsync(companyId, ct);

                if (result is Either<Error, int>.Left failure)
                {
                    logger.LogError(
                        "The electronic invoice retrack of company {CompanyId} failed: {Error}",
                        companyId,
                        failure.Value.Message);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "The electronic invoice retrack of company {CompanyId} failed.",
                    companyId);
            }
        }
    }
}
