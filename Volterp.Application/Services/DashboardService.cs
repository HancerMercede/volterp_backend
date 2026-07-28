using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;

namespace Volterp.Application.Services;

public class DashboardService(IUnitOfWork unitOfWork) : IDashboardService
{
    public async Task<Either<Error, DashboardResponse>> GetDashboardDataAsync(int companyId, CancellationToken ct = default)
    {
        return await EitherAsync<Error, int>
            .FromRight(companyId)
            .Ensure(c => c > 0, new Error("companyId must be greater than zero."))
            .FlatMap(async id => await unitOfWork.Dashboard.GetDashboardDataAsync(id, ct),
                ex => new Error(ex.Message))
            .Run();
    }
}
