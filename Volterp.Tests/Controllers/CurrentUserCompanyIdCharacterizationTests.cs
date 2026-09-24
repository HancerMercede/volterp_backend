using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Volterp.Api.Controllers;
using Volterp.Api.Exceptions;
using Volterp.Api.Helpers;
using Volterp.Application.Common;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Controllers;

public class CurrentUserCompanyIdCharacterizationTests
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

    // ───────── BaseController.GetCurrentUserCompanyId ─────────

    [Fact]
    public void BaseController_GetCurrentUserCompanyId_WhenCompanyIdClaimMissing_ThrowsUnauthorized()
    {
        var sut = BuildWithUser(new TestableBaseController(),
            new Claim(Claims.UserId, "0192b9c7b6a37def8c5e1f2a3b4c5d70"));

        Action act = () => sut.InvokeGetCurrentUserCompanyId();

        act.Should().Throw<UnauthorizedException>(
            "a missing companyId claim MUST surface as 401 — silent zero-fallback is the " +
            "latent bug the Guid 7 migration closes.");
    }

    [Fact]
    public void BaseController_GetCurrentUserCompanyId_WhenCompanyIdClaimPresent_ReturnsParsedGuid()
    {
        var sut = BuildWithUser(new TestableBaseController(),
            new Claim(Claims.CompanyId, "0192b9c7b6a37def8c5e1f2a3b4c5d6e"));

        sut.InvokeGetCurrentUserCompanyId().Should().Be(Guid7TestValues.CompanyA);
    }

    [Fact]
    public void BaseController_GetCurrentUserCompanyId_WhenClaimValueIsMalformed_ThrowsUnauthorized()
    {
        var sut = BuildWithUser(new TestableBaseController(),
            new Claim(Claims.CompanyId, "not-a-guid"));

        Action act = () => sut.InvokeGetCurrentUserCompanyId();

        act.Should().Throw<UnauthorizedException>(
            "a malformed claim MUST surface as 401 — both missing and malformed paths now " +
            "produce the same error contract.");
    }

    [Fact]
    public void BaseController_GetCurrentUserCompanyId_WhenUserIsAnonymous_ThrowsUnauthorized()
    {
        var sut = new TestableBaseController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        Action act = () => sut.InvokeGetCurrentUserCompanyId();

        act.Should().Throw<UnauthorizedException>();
    }

    // ───────── CurrentUserService.GetCompanyId ─────────

    [Fact]
    public void CurrentUserService_GetCompanyId_WhenCompanyIdClaimMissing_ThrowsUnauthorized()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(Claims.UserId, "0192b9c7b6a37def8c5e1f2a3b4c5d70") }, "TestAuth"))
        });
        var sut = new CurrentUserService(accessor.Object);

        Action act = () => sut.GetCompanyId();

        act.Should().Throw<UnauthorizedException>(
            "mirrors BaseController: missing claim MUST surface as 401.");
    }

    [Fact]
    public void CurrentUserService_GetCompanyId_WhenCompanyIdClaimPresent_ReturnsParsedHashCode()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(Claims.CompanyId, "0192b9c7b6a37def8c5e1f2a3b4c5d6e") }, "TestAuth"))
        });
        var sut = new CurrentUserService(accessor.Object);

        sut.GetCompanyId().Should().Be(Guid7TestValues.CompanyA.GetHashCode(),
            "the interface contract returns int; the implementation parses the Guid and " +
            "casts via GetHashCode() because Guid → int has no meaningful mapping.");
    }

    [Fact]
    public void CurrentUserService_GetCompanyId_WhenClaimValueIsMalformed_ThrowsUnauthorized()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(Claims.CompanyId, "abc") }, "TestAuth"))
        });
        var sut = new CurrentUserService(accessor.Object);

        Action act = () => sut.GetCompanyId();

        act.Should().Throw<UnauthorizedException>(
            "both implementations now produce a consistent 401 on malformed claims.");
    }

    [Fact]
    public void CurrentUserService_GetCompanyId_WhenHttpContextIsNull_ThrowsUnauthorized()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var sut = new CurrentUserService(accessor.Object);

        Action act = () => sut.GetCompanyId();

        act.Should().Throw<UnauthorizedException>(
            "no HttpContext means no claim; missing claim MUST surface as 401.");
    }

    // ───────── Both implementations must agree on missing claim ─────────

    [Fact]
    public void BothImplementations_ThrowUnauthorizedOnMissingClaim()
    {
        var baseController = BuildWithUser(new TestableBaseController(),
            new Claim(Claims.UserId, "0192b9c7b6a37def8c5e1f2a3b4c5d70"));

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(Claims.UserId, "0192b9c7b6a37def8c5e1f2a3b4c5d70") }, "TestAuth"))
        });
        var currentUserService = new CurrentUserService(accessor.Object);

        Action fromBase = () => baseController.InvokeGetCurrentUserCompanyId();
        Action fromService = () => currentUserService.GetCompanyId();

        fromBase.Should().Throw<UnauthorizedException>();
        fromService.Should().Throw<UnauthorizedException>(
            "the missing-claim invariant is shared by both implementations; both surface 401 " +
            "with the same exception type so no caller can accidentally fall back to zero.");
    }
}