using Volterp.Application.DTOs;
using Volterp.Application.Interfaces;

namespace Volterp.Application.Services;

public class DashboardService(IUnitOfWork unitOfWork) : IDashboardService
{
    public async Task<DashboardResponse> GetDashboardDataAsync(int companyId, CancellationToken ct = default)
    {
        return await unitOfWork.Dashboard.GetDashboardDataAsync(companyId, ct);
    }
}
