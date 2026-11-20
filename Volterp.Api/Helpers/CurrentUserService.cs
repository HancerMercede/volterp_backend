using System.Security.Claims;
using Volterp.Api.Exceptions;
using Volterp.Application.Common;
using Volterp.Application.Interfaces;
using Volterp.Domain.Enums;

namespace Volterp.Api.Helpers;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    // The user may hold more than one role: the token carries a claim for
    // each one of them.
    public bool IsSuperAdmin()
        => httpContextAccessor.HttpContext?.User.IsInRole(nameof(UserRole.SuperAdmin)) == true;

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
