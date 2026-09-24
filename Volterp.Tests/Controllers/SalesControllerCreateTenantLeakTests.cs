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
/// Cycle 1.1 — Regression tests for the wire refactor that closes bug #1
/// (SalesController.CreateSale ignoring JWT companyId).
///
/// Cycle 1.0 documented the BUGGY behavior. Cycle 1.1 inverts: the controller MUST
/// read the JWT companyId and pass it as an explicit argument to
/// <c>SaleService.CreateSaleAsync</c>. The wire contract also forbids
/// <c>CreateSaleRequest</c> from exposing <c>CompanyId</c> to the client at all;
/// see <c>WireContractTests</c> for that lower-level guard.
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

    // CreateSaleRequest no longer carries CompanyId — the wire cannot set it.
    private static CreateSaleRequest BuildCreateSaleRequest() =>
        new(
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
    public async Task CreateSale_PassesJwtCompanyIdToService_NotAnyRequestValue()
    {
        // ARRANGE — fixed behavior in Cycle 1.1:
        //   The controller reads the JWT companyId (999) and passes it as an explicit
        //   argument to SaleService.CreateSaleAsync. The request body cannot carry
        //   CompanyId anymore — CreateSaleRequest has no CompanyId property.
        int capturedCompanyId = 0;
        int? capturedUserId = null;
        CreateSaleRequest? capturedRequest = null;

        var mockServiceManager = new Mock<IServiceManager>();
        mockServiceManager
            .Setup(s => s.Sales.CreateSaleAsync(
                It.IsAny<CreateSaleRequest>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreateSaleRequest, int, int?, CancellationToken>((req, cid, uid, _) =>
            {
                capturedRequest = req;
                capturedCompanyId = cid;
                capturedUserId = uid;
            })
            .ReturnsAsync(new Either<Error, SaleDto>.Right(new SaleDto { Id = 1, CompanyId = JwtCompanyId }));

        var controller = BuildController(mockServiceManager.Object, JwtCompanyId);
        var request = BuildCreateSaleRequest();

        // ACT
        _ = await controller.CreateSale(request, CancellationToken.None);

        // ASSERT — fixed behavior.
        capturedRequest.Should().NotBeNull();
        capturedCompanyId.Should().Be(JwtCompanyId,
            "Cycle 1.1 fix: the controller must read the JWT companyId and pass it as an " +
            "explicit argument to SaleService.CreateSaleAsync. The request body cannot " +
            "carry CompanyId anymore — only the JWT-derived tenant defines the scope.");
        capturedUserId.Should().Be(7,
            "the JWT userId is still passed through unchanged.");

        // CreateSaleRequest must not expose CompanyId to the wire — the type itself
        // is the contract. Reflection check is more durable than JSON deserialization.
        typeof(CreateSaleRequest)
            .GetProperty("CompanyId")
            .Should()
            .BeNull("CreateSaleRequest is request-body DTO; tenant scope comes from the JWT only.");
    }

    [Fact]
    public async Task CreateSale_WhenClientHasNoWayToSetCompanyId_ExecutionIsBoundedToJwtTenant()
    {
        // ARRANGE — simulates a malicious client trying to bypass tenant enforcement
        // via the wire. After the wire refactor:
        //   - CreateSaleRequest has no CompanyId at all.
        //   - The service gets called with the JWT companyId (999), not 42.
        // The cross-tenant write attempt is structurally impossible.
        int capturedCompanyId = 0;

        var mockServiceManager = new Mock<IServiceManager>();
        mockServiceManager
            .Setup(s => s.Sales.CreateSaleAsync(
                It.IsAny<CreateSaleRequest>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreateSaleRequest, int, int?, CancellationToken>((_, cid, _, _) => capturedCompanyId = cid)
            .ReturnsAsync(new Either<Error, SaleDto>.Right(new SaleDto { Id = 1, CompanyId = JwtCompanyId }));

        var controller = BuildController(mockServiceManager.Object, JwtCompanyId);
        var request = BuildCreateSaleRequest();

        // ACT — the client cannot influence tenant scope via the body anymore.
        _ = await controller.CreateSale(request, CancellationToken.None);

        // ASSERT
        capturedCompanyId.Should().Be(JwtCompanyId);
        capturedCompanyId.Should().NotBe(RequestCompanyId,
            "the controller MUST bind tenant scope to the JWT claim, not to anything the " +
            "client controls.");
    }

    [Fact]
    public async Task CreateSale_OnHappyPath_PassesJwtCompanyIdAndUserIdThrough()
    {
        // ARRANGE — happy-path control case (Cycle 1.0 analog: when JWT and
        // request.CompanyId matched, the original behavior was correct). After the
        // wire refactor: there is no request.CompanyId, only the JWT — but the
        // controller must still bind the service to the JWT-derived tenant and
        // pass the userId through unchanged.
        int capturedCompanyId = 0;
        int? capturedUserId = null;

        var mockServiceManager = new Mock<IServiceManager>();
        mockServiceManager
            .Setup(s => s.Sales.CreateSaleAsync(
                It.IsAny<CreateSaleRequest>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreateSaleRequest, int, int?, CancellationToken>((_, cid, uid, _) =>
            {
                capturedCompanyId = cid;
                capturedUserId = uid;
            })
            .ReturnsAsync(new Either<Error, SaleDto>.Right(new SaleDto { Id = 1, CompanyId = JwtCompanyId }));

        var controller = BuildController(mockServiceManager.Object, JwtCompanyId);
        var request = BuildCreateSaleRequest();

        // ACT
        _ = await controller.CreateSale(request, CancellationToken.None);

        // ASSERT — fixed behavior, happy path.
        capturedCompanyId.Should().Be(JwtCompanyId);
        capturedUserId.Should().Be(7,
            "userId propagation is unchanged by the wire refactor — only the tenant binding moved.");
    }
}
