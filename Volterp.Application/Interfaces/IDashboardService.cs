using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

public interface IDashboardService
{
    Task<Either<Error, DashboardResponse>> GetDashboardDataAsync(int companyId, CancellationToken ct = default);
}
