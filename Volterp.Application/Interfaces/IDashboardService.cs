using Volterp.Application.DTOs;

namespace Volterp.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardResponse> GetDashboardDataAsync(int companyId, CancellationToken ct = default);
}
