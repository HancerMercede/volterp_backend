using System.Security.Claims;
using Volterp.Application.Interfaces;

namespace Volterp.Api.Helpers;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public bool IsSuperAdmin()
    {
        var role = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value?.ToLower();
        return role == "superadmin";
    }

    public int GetCompanyId()
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirst("companyId")?.Value ?? "0";
        return int.Parse(value);
    }

    public int? GetUserId()
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
        return value is not null && int.TryParse(value, out var id) ? id : null;
    }
}