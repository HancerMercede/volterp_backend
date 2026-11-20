using Microsoft.Extensions.DependencyInjection;
using Volterp.Api.Exceptions;
using Volterp.Application.Abstractions;
using Volterp.Application.Common;
using Volterp.Domain.Enums;

namespace Volterp.Api.Controllers;

public abstract class BaseController : ControllerBase
{
    protected Guid GetCurrentUserCompanyId()
    {
        var id = HttpContext.RequestServices.GetRequiredService<ITenantContext>().CompanyId;
        return id ?? throw new UnauthorizedException("Missing companyId claim.");
    }

    protected void EnsureTenant() => GetCurrentUserCompanyId();

    protected Guid? GetCurrentUserId()
    {
        var raw = User.FindFirst(Claims.UserId)?.Value;
        if (string.IsNullOrEmpty(raw))
            return null;
        return Guid.TryParseExact(raw, "N", out var id) ? id : null;
    }

    // The token carries one claim per role the user holds, so the roles of the
    // caller are every claim of the role type and not only the first one.
    protected bool IsAdmin()
        => User.IsInRole(nameof(UserRole.Admin)) || User.IsInRole(nameof(UserRole.SuperAdmin));

    protected bool IsSuperAdminOnly()
        => User.IsInRole(nameof(UserRole.SuperAdmin));
}
