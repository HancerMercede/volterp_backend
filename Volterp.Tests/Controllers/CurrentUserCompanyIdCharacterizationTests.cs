using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Volterp.Api.Controllers;
using Volterp.Api.Helpers;
using Xunit;

namespace Volterp.Tests.Controllers;

/// <summary>
/// Cycle 1.0 — Characterization tests for the two parallel implementations of
/// "current user companyId resolution":
///
/// 1. <c>BaseController.GetCurrentUserCompanyId()</c> — used by every controller
///    action via inheritance.
/// 2. <c>CurrentUserService.GetCompanyId()</c> — the registered
///    <c>ICurrentUserService</c> implementation (currently bypassed by all
///    controllers but consumed by <c>UserService</c> and <c>ServiceManager</c>).
///
/// Both implementations share the same flaw: when the <c>companyId</c> claim is
/// missing, they return <c>0</c> silently. The plan calls for a 401 in that
/// case (Phase 1 acceptance criteria). These tests document the current
/// behavior so the fix in Cycle 1.3 is enforced by regression.
/// </summary>
public class CurrentUserCompanyIdCharacterizationTests
{
    /// <summary>
    /// Concrete subclass that exposes the protected helper for direct testing
    /// without spinning up the full MVC pipeline.
    /// </summary>
    private sealed class TestableBaseController : BaseController
    {
        public int InvokeGetCurrentUserCompanyId() => GetCurrentUserCompanyId();
        public int? InvokeGetCurrentUserId() => GetCurrentUserId();
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
    public void BaseController_GetCurrentUserCompanyId_WhenCompanyIdClaimMissing_ReturnsZero()
    {
        // ARRANGE — User has no companyId claim at all.
        var sut = BuildWithUser(new TestableBaseController(), new Claim("userId", "7"));

        // ACT
        var result = sut.InvokeGetCurrentUserCompanyId();

        // ASSERT — current behavior (silent failure).
        result.Should().Be(0,
            "CURRENT BEHAVIOR: when the companyId claim is missing, the helper returns 0 silently. " +
            "Cycle 1.3 will flip this to throw 401 Unauthorized.");
    }

    [Fact]
    public void BaseController_GetCurrentUserCompanyId_WhenCompanyIdClaimPresent_ReturnsParsedValue()
    {
        // ARRANGE
        var sut = BuildWithUser(new TestableBaseController(), new Claim("companyId", "555"));

        // ACT
        var result = sut.InvokeGetCurrentUserCompanyId();

        // ASSERT
        result.Should().Be(555);
    }

    [Fact]
    public void BaseController_GetCurrentUserCompanyId_WhenClaimValueIsMalformed_Throws()
    {
        // ARRANGE — claim value cannot be parsed as int.
        var sut = BuildWithUser(new TestableBaseController(), new Claim("companyId", "not-a-number"));

        // ACT
        Action act = () => sut.InvokeGetCurrentUserCompanyId();

        // ASSERT — current behavior: int.Parse throws FormatException.
        act.Should().Throw<FormatException>(
            "CURRENT BEHAVIOR: int.Parse on a malformed claim bubbles up uncaught. " +
            "Cycle 1.3 should validate the claim and return 401 instead.");
    }

    [Fact]
    public void BaseController_GetCurrentUserCompanyId_WhenUserIsAnonymous_ReturnsZero()
    {
        // ARRANGE — no authenticated user at all.
        var sut = new TestableBaseController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // ACT
        var result = sut.InvokeGetCurrentUserCompanyId();

        // ASSERT
        result.Should().Be(0);
    }

    // ───────── CurrentUserService.GetCompanyId ─────────

    [Fact]
    public void CurrentUserService_GetCompanyId_WhenCompanyIdClaimMissing_ReturnsZero()
    {
        // ARRANGE
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userId", "1") }, "TestAuth"))
        });
        var sut = new CurrentUserService(accessor.Object);

        // ACT
        var result = sut.GetCompanyId();

        // ASSERT — mirrors BaseController behavior.
        result.Should().Be(0,
            "CURRENT BEHAVIOR: CurrentUserService silently returns 0 when the claim is missing. " +
            "Same fix as BaseController applies in Cycle 1.3.");
    }

    [Fact]
    public void CurrentUserService_GetCompanyId_WhenCompanyIdClaimPresent_ReturnsParsedValue()
    {
        // ARRANGE
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("companyId", "777") }, "TestAuth"))
        });
        var sut = new CurrentUserService(accessor.Object);

        // ACT
        var result = sut.GetCompanyId();

        // ASSERT
        result.Should().Be(777);
    }

    [Fact]
    public void CurrentUserService_GetCompanyId_WhenClaimValueIsMalformed_Throws()
    {
        // ARRANGE
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("companyId", "abc") }, "TestAuth"))
        });
        var sut = new CurrentUserService(accessor.Object);

        // ACT
        Action act = () => sut.GetCompanyId();

        // ASSERT
        act.Should().Throw<FormatException>(
            "CURRENT BEHAVIOR: CurrentUserService uses int.Parse without TryParse, same as BaseController. " +
            "Cycle 1.3 should consolidate both implementations behind ITenantContext and validate.");
    }

    [Fact]
    public void CurrentUserService_GetCompanyId_WhenHttpContextIsNull_ReturnsZero()
    {
        // ARRANGE — accessor returns null HttpContext (e.g. outside a request).
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var sut = new CurrentUserService(accessor.Object);

        // ACT
        var result = sut.GetCompanyId();

        // ASSERT
        result.Should().Be(0);
    }

    // ───────── Both implementations must agree ─────────

    [Fact]
    public void BothImplementations_ReturnSameValueForSameClaim()
    {
        // ARRANGE
        var companyId = "12345";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("companyId", companyId) }, "TestAuth"));

        var baseController = BuildWithUser(new TestableBaseController(), new Claim("companyId", companyId));

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext { User = principal });
        var service = new CurrentUserService(accessor.Object);

        // ACT
        var fromBase = baseController.InvokeGetCurrentUserCompanyId();
        var fromService = service.GetCompanyId();

        // ASSERT — both return the same value when the claim is present.
        // Cycle 1.3 will consolidate these two behind ITenantContext.
        fromBase.Should().Be(fromService);
        fromBase.Should().Be(12345);
    }
}
