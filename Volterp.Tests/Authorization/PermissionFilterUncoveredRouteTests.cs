using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Volterp.Api.Authorization;
using Volterp.Api.Controllers;
using Xunit;

namespace Volterp.Tests.Authorization;

// The deny-by-default: a route that nobody declared what it needs stays
// closed. The inventory test says that every route of the API is declared;
// this one says what happens when one is not.
public class PermissionFilterUncoveredRouteTests
{
    [Fact]
    public async Task TheRouteThatTheMapDoesNotCoverIsClosed()
    {
        var (filter, context) = Build("DELETE", "SuperAdmin");

        await filter.OnAuthorizationAsync(context);

        context.Result.Should().NotBeNull(
            "a route that the map does not cover is a route that anybody authenticated could call");

        (context.Result as ObjectResult)?.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task TheRouteThatTheMapCoversIsLeftToTheMatrix()
    {
        // The dashboard is covered and the role of the caller has the
        // permission: the filter does not refuse it.
        var (filter, context) = Build("GET", "Vendedor");

        await filter.OnAuthorizationAsync(context);

        context.Result.Should().BeNull(
            "the map covers this route and the role of the caller has the permission");
    }

    [Fact]
    public async Task TheRouteThatTheMapCoversIsRefusedWhenTheRoleDoesNotHaveIt()
    {
        // The dashboard is covered, but the role of the caller does not have
        // the permission of it.
        var (filter, context) = Build("GET", "RRHH");

        await filter.OnAuthorizationAsync(context);

        (context.Result as ObjectResult)?.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // The dashboard has no DELETE in the map: that is the route that nobody
    // declared.
    private static (PermissionAuthorizationFilter Filter, AuthorizationFilterContext Context) Build(
        string verb,
        string role)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = verb;
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test", ClaimTypes.Name, ClaimTypes.Role));

        var action = new ControllerActionDescriptor
        {
            ActionName = nameof(DashboardController.GetDashboard),
            ControllerName = "Dashboard",
            ControllerTypeInfo = typeof(DashboardController).GetTypeInfo(),
            MethodInfo = typeof(DashboardController).GetMethod(nameof(DashboardController.GetDashboard))!
        };

        action.EndpointMetadata = new List<object> { new AuthorizeAttribute() };

        var actionContext = new ActionContext(httpContext, new RouteData(), action);

        var filter = new PermissionAuthorizationFilter(
            new HttpContextAccessor { HttpContext = httpContext });

        return (filter, new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>()));
    }
}
