using Volterp.Application.DTOs;

namespace Volterp.Application.Interfaces;

public interface IDashboardRepository
{
    Task<DashboardResponse> GetDashboardDataAsync(CancellationToken ct = default);
}
