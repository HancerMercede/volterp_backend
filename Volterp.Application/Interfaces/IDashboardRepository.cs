using Volterp.Application.DTOs;

namespace Volterp.Application.Interfaces;

public interface IDashboardRepository
{
    Task<DashboardResponse> GetDashboardDataAsync(int companyId, CancellationToken ct = default);
}
