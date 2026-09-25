using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Api.Controllers;
using Volterp.Api.Exceptions;
using Volterp.Api.Http;
using Volterp.Application.Abstractions;
using Volterp.Application.Common;
using Xunit;

namespace Volterp.Tests.Controllers;

public class BaseControllerAuthorizationTests
{
    private sealed class TestableBaseController : BaseController
    {
        public Guid InvokeGetCurrentUserCompanyId() => GetCurrentUserCompanyId();
        public Guid? InvokeGetCurrentUserId() => GetCurrentUserId();
    }

    private static TController BuildWithUser<TController>(TController controller, params Claim[] claims)
        where TController : ControllerBase
    {
        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor>(accessor);
        services.AddSingleton<ITenantContext>(new HttpTenantContext(accessor));
        httpContext.RequestServices = services.BuildServiceProvider();

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public void BaseController_GetCurrentUserCompanyId_WhenClaimMissing_ThrowsUnauthorized()
    {
        var sut = BuildWithUser(new TestableBaseController(), new Claim(Claims.UserId, "0192b9c7b6a37def8c5e1f2a3b4c5d70"));

        Action act = () => sut.InvokeGetCurrentUserCompanyId();

        act.Should().Throw<UnauthorizedException>();
    }

    [Fact]
    public void BaseController_GetCurrentUserCompanyId_WhenClaimMalformed_ThrowsUnauthorized()
    {
        var sut = BuildWithUser(new TestableBaseController(), new Claim(Claims.CompanyId, "not-a-guid"));

        Action act = () => sut.InvokeGetCurrentUserCompanyId();

        act.Should().Throw<UnauthorizedException>();
    }
}