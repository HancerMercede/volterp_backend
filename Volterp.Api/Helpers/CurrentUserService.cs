using System.Security.Claims;
using Volterp.Api.Exceptions;
using Volterp.Application.Common;
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
        var raw = httpContextAccessor.HttpContext?.User.FindFirst(Claims.CompanyId)?.Value;
        if (string.IsNullOrEmpty(raw))
            throw new UnauthorizedException("Missing companyId claim.");
        if (!Guid.TryParseExact(raw, "N", out var id))
            throw new UnauthorizedException("Malformed companyId claim.");
        return id.GetHashCode();
    }

    public int? GetUserId()
    {
        var raw = httpContextAccessor.HttpContext?.User.FindFirst(Claims.UserId)?.Value;
        if (string.IsNullOrEmpty(raw))
            return null;
        if (!Guid.TryParseExact(raw, "N", out var id))
            throw new UnauthorizedException("Malformed userId claim.");
        return id.GetHashCode();
    }
}