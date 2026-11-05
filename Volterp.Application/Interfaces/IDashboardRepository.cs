using Volterp.Application.DTOs;
using Volterp.Domain.Enums;

namespace Volterp.Application.Interfaces;

public interface IDashboardRepository
{
    Task<DashboardResponse> GetDashboardDataAsync(
        DashboardScope scope,
        ChartWindow chartWindow,
        CancellationToken ct = default);
}
