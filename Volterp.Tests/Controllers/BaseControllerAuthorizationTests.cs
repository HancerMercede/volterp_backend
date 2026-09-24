using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volterp.Api.Controllers;
using Volterp.Api.Exceptions;
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
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
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