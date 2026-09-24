using Volterp.Api.Exceptions;
using Volterp.Application.Common;

namespace Volterp.Api.Controllers;

public abstract class BaseController : ControllerBase
{
    protected Guid GetCurrentUserCompanyId()
    {
        var raw = User.FindFirst(Claims.CompanyId)?.Value;
        if (string.IsNullOrEmpty(raw))
            throw new UnauthorizedException("Missing companyId claim.");
        if (!Guid.TryParseExact(raw, "N", out var id))
            throw new UnauthorizedException("Malformed companyId claim.");
        return id;
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