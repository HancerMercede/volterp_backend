using System.Security.Claims;
using EitherWay;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Volterp.Api.Controllers;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Enums;
using Xunit;

namespace Volterp.Tests.Controllers;

/// <summary>
/// Cycle 1.0 — Characterization tests for cross-tenant bugs identified in
/// <c>odd/tasks/multi-tenant-isolation.md</c>.
///
/// Bug #1: <c>SalesController.CreateSale</c> ignores the JWT companyId claim and
/// forwards <c>request.CompanyId</c> to <c>SaleService.CreateSaleAsync</c>. The
/// method signature of the service doesn't even accept a companyId parameter,
/// which makes the JWT-derived tenant invisible at the application layer.
///
/// These tests document the CURRENT broken behavior. They pass today. When
/// the bug is fixed in Cycle 1.1, each assertion below must be inverted (or
/// the test removed and replaced with a positive-path test) so that the fix
/// is enforced by regression.
/// </summary>
public class SalesControllerCreateTenantLeakTests
{
    private const int JwtCompanyId = 999;
    private const int RequestCompanyId = 42;

    private static SalesController BuildController(IServiceManager serviceManager, int jwtCompanyId)
    {
        var controller = new SalesController(serviceManager);

        var claims = new List<Claim>
        {
            new("userId", "7"),
            new("companyId", jwtCompanyId.ToString()),
            new(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    private static CreateSaleRequest BuildCreateSaleRequest(int companyId) =>
        new(
            CompanyId: companyId,
            ClienteId: null,
            ClienteName: "Test Client",
            Status: SaleStatus.Pending,
            Total: 500m,
            Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(ProductId: 1, ProductName: "Widget", ProductCategory: null,
                    ProductCode: null, ProductImageUrl: null,
                    Quantity: 5, UnitPrice: 100m, Subtotal: 500m)
            }
        );

    [Fact]
    public async Task CreateSale_RequestCompanyIdDiffersFromJwtCompanyId_PassesRequestCompanyIdToService()
    {
        // ARRANGE
        // The JWT carries companyId=999, but the client sends companyId=42 in the body.
        // Current bug: the service receives the request's CompanyId (42), not the JWT's (999).
        CreateSaleRequest? capturedRequest = null;
        var mockServiceManager = new Mock<IServiceManager>();
        mockServiceManager
            .Setup(s => s.Sales.CreateSaleAsync(It.IsAny<CreateSaleRequest>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<CreateSaleRequest, int?, CancellationToken>((req, _, _) => capturedRequest = req)
            .ReturnsAsync(new Either<Error, SaleDto>.Right(new SaleDto { Id = 1, CompanyId = RequestCompanyId }));

        var controller = BuildController(mockServiceManager.Object, JwtCompanyId);
        var request = BuildCreateSaleRequest(RequestCompanyId);

        // ACT
        var result = await controller.CreateSale(request, CancellationToken.None);

        // ASSERT — current behavior (bug).
        // When the fix is applied, the controller must override request.CompanyId with the
        // JWT-derived companyId (999). At that point this assertion must change.
        capturedRequest.Should().NotBeNull("controller forwards the request to the service");
        capturedRequest!.CompanyId.Should().Be(RequestCompanyId,
            "CURRENT BUG: the controller forwards request.CompanyId, ignoring the JWT claim. " +
            "After Cycle 1.1 this must become JwtCompanyId (999).");

        // Status is 201 Created — the bug means the cross-tenant write is not blocked.
        // ActionResult<T> wraps the inner IActionResult; assert against .Result.
        result.Result.Should().BeOfType<CreatedAtActionResult>(
            "the request succeeds despite carrying a CompanyId that does not belong to the JWT user");
    }

    [Fact]
    public async Task CreateSale_RequestCompanyIdMatchesJwtCompanyId_PassesRequestCompanyIdToService()
    {
        // ARRANGE — happy-path control case. Both sides agree.
        CreateSaleRequest? capturedRequest = null;
        var mockServiceManager = new Mock<IServiceManager>();
        mockServiceManager
            .Setup(s => s.Sales.CreateSaleAsync(It.IsAny<CreateSaleRequest>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<CreateSaleRequest, int?, CancellationToken>((req, _, _) => capturedRequest = req)
            .ReturnsAsync(new Either<Error, SaleDto>.Right(new SaleDto { Id = 1, CompanyId = JwtCompanyId }));

        var controller = BuildController(mockServiceManager.Object, JwtCompanyId);
        var request = BuildCreateSaleRequest(JwtCompanyId);

        // ACT
        _ = await controller.CreateSale(request, CancellationToken.None);

        // ASSERT — current behavior is correct in this case.
        capturedRequest.Should().NotBeNull();
        capturedRequest!.CompanyId.Should().Be(JwtCompanyId);
    }

    [Fact]
    public async Task CreateSale_InvokesServiceWithRequestCompanyId_NotJwtCompanyId()
    {
        // ARRANGE — explicit assertion on the value flow that enables the leak.
        const int capturedCompanyId = 7; // arbitrary request value
        CreateSaleRequest? capturedRequest = null;
        var mockServiceManager = new Mock<IServiceManager>();
        mockServiceManager
            .Setup(s => s.Sales.CreateSaleAsync(It.IsAny<CreateSaleRequest>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<CreateSaleRequest, int?, CancellationToken>((req, _, _) => capturedRequest = req)
            .ReturnsAsync(new Either<Error, SaleDto>.Right(new SaleDto { Id = 1, CompanyId = capturedCompanyId }));

        var controller = BuildController(mockServiceManager.Object, jwtCompanyId: 1234);
        var request = BuildCreateSaleRequest(capturedCompanyId);

        // ACT
        await controller.CreateSale(request, CancellationToken.None);

        // ASSERT
        capturedRequest.Should().NotBeNull();
        capturedRequest!.CompanyId.Should().NotBe(1234,
            "CURRENT BUG: the controller does not look up the JWT companyId and forwards the request's. " +
            "This test will need to be removed or flipped in Cycle 1.1.");
    }
}
