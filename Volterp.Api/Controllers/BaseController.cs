using Microsoft.Extensions.DependencyInjection;
using Volterp.Api.Exceptions;
using Volterp.Application.Abstractions;
using Volterp.Application.Common;

namespace Volterp.Api.Controllers;

public abstract class BaseController : ControllerBase
{
    protected Guid GetCurrentUserCompanyId()
    {
        var id = HttpContext.RequestServices.GetRequiredService<ITenantContext>().CompanyId;
        return id ?? throw new UnauthorizedException("Missing companyId claim.");
    }

    protected Guid? GetCurrentUserId()
    {
        var raw = User.FindFirst(Claims.UserId)?.Value;
        if (string.IsNullOrEmpty(raw))
            return null;
        return Guid.TryParseExact(raw, "N", out var id) ? id : null;
    }

    protected bool IsAdmin()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value.ToLower();
        return role == "admin" || role == "superadmin";
    }

    protected bool IsSuperAdminOnly()
        => User.FindFirst(ClaimTypes.Role)?.Value.ToLower() == "superadmin";
}