using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Domain.Enums;

namespace Volterp.Application.Interfaces;

public interface IDashboardService
{
    Task<Either<Error, DashboardResponse>> GetDashboardDataAsync(
        DashboardScope scope,
        ChartWindow chartWindow,
        CancellationToken ct = default);
}
