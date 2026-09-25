using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;

namespace Volterp.Application.Services;

public class DashboardService(IUnitOfWork unitOfWork) : IDashboardService
{
    public async Task<Either<Error, DashboardResponse>> GetDashboardDataAsync(CancellationToken ct = default)
    {
        return await EitherAsync<Error, Unit>
            .FromRight(new Unit())
            .FlatMap(async _ => await unitOfWork.Dashboard.GetDashboardDataAsync(ct),
                ex => new Error(ex.Message))
            .Run();
    }
}
