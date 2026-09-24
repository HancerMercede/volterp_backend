using EitherWay;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

public interface IDashboardService
{
    Task<Either<Error, DashboardResponse>> GetDashboardDataAsync(Guid companyId, CancellationToken ct = default);
}
