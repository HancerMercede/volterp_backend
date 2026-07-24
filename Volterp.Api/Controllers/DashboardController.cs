using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(IServiceManager serviceManager) : BaseController
{
    [HttpGet]
    public async Task<ActionResult> GetDashboard(CancellationToken ct)
    {
        var companyId = GetCurrentUserCompanyId();
        var response = await serviceManager.Dashboard.GetDashboardDataAsync(companyId, ct);
        return Ok(response);
    }
}
