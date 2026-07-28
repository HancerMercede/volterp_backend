using EitherWay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volterp.Application.DTOs;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(IServiceManager serviceManager) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> GetDashboard(CancellationToken ct)
    {
        var companyId = GetCurrentUserCompanyId();
        var result = await serviceManager.Dashboard.GetDashboardDataAsync(companyId, ct);
        return result.Match<ActionResult<DashboardResponse>>(
            error => BadRequest(new ErrorResponse(error.Message)),
            data => Ok(data)
        );
    }
}
