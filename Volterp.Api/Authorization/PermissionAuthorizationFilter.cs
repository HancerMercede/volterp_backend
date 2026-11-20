using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Volterp.Application.DTOs;
using Volterp.Domain.Authorization;
using Volterp.Domain.Enums;

namespace Volterp.Api.Authorization;

// The authorization of every route: the route says what it needs (the map of
// the routes) and the matrix (the roles and what they grant) says who gets
// in.
//
// A route that the map does not cover is closed. That is the deny-by-default:
// a route that nobody declared is a route that anybody authenticated could
// call, and the inventory test fails when one is missing from the map.
public class PermissionAuthorizationFilter(IHttpContextAccessor httpContextAccessor)
    : IAsyncAuthorizationFilter
{
    private const string JwtSchemeName = "JwtBearer";

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // The routes that a token does not protect are not ours: the public
        // ones and the ones that a third party enters with its API key.
        if (!IsTokenProtected(context))
            return Task.CompletedTask;

        if (context.ActionDescriptor is not ControllerActionDescriptor action)
            return Task.CompletedTask;

        // The map knows the routes of this API. The controllers of another
        // assembly (the ones the suite brings) are not ours to authorize.
        if (action.ControllerTypeInfo.Assembly != typeof(PermissionAuthorizationFilter).Assembly)
            return Task.CompletedTask;

        var permission = PermissionRoutes.For(
            action.ControllerTypeInfo,
            context.HttpContext.Request.Method,
            action.ActionName);

        if (permission is null)
        {
            context.Result = Forbidden(
                $"The route '{context.HttpContext.Request.Path}' does not declare the permission it needs.");

            return Task.CompletedTask;
        }

        var user = httpContextAccessor.HttpContext?.User;

        if (user is null || user.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        // The callers that enter with an API key are not the user: their
        // principal carries no role, and the matrix has nothing to say about
        // them. They keep the access they had.
        if (!user.FindAll(ClaimTypes.Role).Any())
            return Task.CompletedTask;

        if (RolePermissions.For(RolesOf(user)).Contains(permission.Value))
            return Task.CompletedTask;

        context.Result = Forbidden($"You do not have the permission '{permission.Value.Key()}'.");

        return Task.CompletedTask;
    }

    // The roles the caller holds. The token carries one claim per role, and a
    // claim that is not a role of the catalog is ignored: a caller with a
    // role nobody knows holds no permissions.
    private static List<UserRole> RolesOf(ClaimsPrincipal user)
        => user.FindAll(ClaimTypes.Role)
            .Select(claim => Enum.TryParse<UserRole>(claim.Value, ignoreCase: true, out var role)
                ? role
                : (UserRole?)null)
            .Where(role => role.HasValue)
            .Select(role => role!.Value)
            .ToList();

    // The route is protected by a token when it asks for the authorization and
    // does not ask for another scheme: the routes that a third party enters
    // with its API key are not the user's.
    private static bool IsTokenProtected(AuthorizationFilterContext context)
    {
        var requirements = context.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .ToList();

        if (requirements.Count == 0)
            return false;

        return requirements.Any(requirement =>
            string.IsNullOrEmpty(requirement.AuthenticationSchemes)
            || requirement.AuthenticationSchemes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(JwtSchemeName, StringComparer.OrdinalIgnoreCase));
    }

    private static ObjectResult Forbidden(string message)
        => new(new ErrorResponse(message)) { StatusCode = StatusCodes.Status403Forbidden };
}