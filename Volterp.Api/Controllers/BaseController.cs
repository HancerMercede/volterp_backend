using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Volterp.Api.Controllers;

public abstract class BaseController : ControllerBase
{
    protected int GetCurrentUserCompanyId()
        => int.Parse(User.FindFirst("companyId")?.Value ?? "0");

    protected int? GetCurrentUserId()
        => int.TryParse(User.FindFirst("userId")?.Value, out var id) ? id : null;

    protected bool IsAdmin()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value.ToLower();
        return role == "admin" || role == "superadmin";
    }

    protected bool IsSuperAdminOnly()
        => User.FindFirst(ClaimTypes.Role)?.Value.ToLower() == "superadmin";
}