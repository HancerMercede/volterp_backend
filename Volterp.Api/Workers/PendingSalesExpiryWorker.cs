using EitherWay;
using Volterp.Api.Configuration;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Workers;

// The pending sales that nobody decided expire: the sweep closes them and
// the stock they reserved comes back.
public sealed class PendingSalesExpiryWorker(
    IServiceScopeFactory scopeFactory,
    PendingSalesOptions options,
    TimeProvider timeProvider,
    IHostEnvironment hostEnvironment,
    ILogger<PendingSalesExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (hostEnvironment.IsEnvironment("Testing"))
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The pending sales could not be expired.");
            }

            try
            {
                await Task.Delay(options.SweepInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task ExpireAsync(CancellationToken ct)
    {
        // The sweep runs without an HTTP request: the company of each sale
        // travels by hand.
        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider.GetRequiredService<IServiceManager>();

        var result = await services.Sales.ExpirePendingSalesAsync(options.Lifetime, ct);

        if (result is Either<Error, int>.Left failure)
        {
            logger.LogError("The sweep of the pending sales failed: {Error}", failure.Value.Message);
            return;
        }

        var closed = ((Either<Error, int>.Right)result).Value;

        if (closed > 0)
            logger.LogInformation("The sweep closed {Count} pending sales.", closed);
    }
}
