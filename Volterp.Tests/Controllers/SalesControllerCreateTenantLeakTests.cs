using System.Security.Claims;
using EitherWay;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Volterp.Api.Controllers;
using Volterp.Application.Common;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Enums;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Controllers;

public class SalesControllerCreateTenantLeakTests
{
    private static readonly Guid JwtCompanyId = Guid7TestValues.CompanyA;
    private static readonly Guid RequestCompanyId = Guid7TestValues.CompanyB;
    private static readonly Guid JwtUserId = Guid7TestValues.UserA;

    private static SalesController BuildController(IServiceManager serviceManager, Guid jwtCompanyId, Guid jwtUserId)
    {
        var controller = new SalesController(serviceManager);

        var claims = new List<Claim>
        {
            new(Claims.UserId, jwtUserId.ToString("N")),
            new(Claims.CompanyId, jwtCompanyId.ToString("N")),
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

    private static CreateSaleRequest BuildCreateSaleRequest() =>
        new(
            ClienteId: null,
            ClienteName: "Test Client",
            Status: SaleStatus.Pending,
            Total: 500m,
            Notes: null,
            Items: new List<CreateSaleItemRequest>
            {
                new(ProductId: Guid7TestValues.ProductA, ProductName: "Widget", ProductCategory: null,
                    ProductCode: null, ProductImageUrl: null,
                    Quantity: 5, UnitPrice: 100m, Subtotal: 500m)
            }
        );

    [Fact]
    public async Task CreateSale_PassesJwtCompanyIdToService_NotAnyRequestValue()
    {
        Guid capturedCompanyId = Guid.Empty;
        Guid? capturedUserId = null;
        CreateSaleRequest? capturedRequest = null;

        var mockServiceManager = new Mock<IServiceManager>();
        mockServiceManager
            .Setup(s => s.Sales.CreateSaleAsync(
                It.IsAny<CreateSaleRequest>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreateSaleRequest, Guid, Guid?, CancellationToken>((req, cid, uid, _) =>
            {
                capturedRequest = req;
                capturedCompanyId = cid;
                capturedUserId = uid;
            })
            .ReturnsAsync(new Either<Error, SaleDto>.Right(new SaleDto
                { Id = Guid7TestValues.SaleA, CompanyId = JwtCompanyId }));

        var controller = BuildController(mockServiceManager.Object, JwtCompanyId, JwtUserId);
        var request = BuildCreateSaleRequest();

        _ = await controller.CreateSale(request, CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedCompanyId.Should().Be(JwtCompanyId,
            "the controller must read the JWT companyId and pass it as an explicit " +
            "argument to SaleService.CreateSaleAsync. The request body cannot " +
            "carry CompanyId anymore — only the JWT-derived tenant defines the scope.");
        capturedUserId.Should().Be(JwtUserId,
            "the JWT userId is still passed through unchanged.");

        typeof(CreateSaleRequest)
            .GetProperty("CompanyId")
            .Should()
            .BeNull("CreateSaleRequest is request-body DTO; tenant scope comes from the JWT only.");
    }

    [Fact]
    public async Task CreateSale_WhenClientHasNoWayToSetCompanyId_ExecutionIsBoundedToJwtTenant()
    {
        Guid capturedCompanyId = Guid.Empty;

        var mockServiceManager = new Mock<IServiceManager>();
        mockServiceManager
            .Setup(s => s.Sales.CreateSaleAsync(
                It.IsAny<CreateSaleRequest>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreateSaleRequest, Guid, Guid?, CancellationToken>((_, cid, _, _) => capturedCompanyId = cid)
            .ReturnsAsync(new Either<Error, SaleDto>.Right(new SaleDto
                { Id = Guid7TestValues.SaleA, CompanyId = JwtCompanyId }));

        var controller = BuildController(mockServiceManager.Object, JwtCompanyId, JwtUserId);
        var request = BuildCreateSaleRequest();

        _ = await controller.CreateSale(request, CancellationToken.None);

        capturedCompanyId.Should().Be(JwtCompanyId);
        capturedCompanyId.Should().NotBe(RequestCompanyId,
            "the controller MUST bind tenant scope to the JWT claim, not to anything the " +
            "client controls.");
    }

    [Fact]
    public async Task CreateSale_OnHappyPath_PassesJwtCompanyIdAndUserIdThrough()
    {
        Guid capturedCompanyId = Guid.Empty;
        Guid? capturedUserId = null;

        var mockServiceManager = new Mock<IServiceManager>();
        mockServiceManager
            .Setup(s => s.Sales.CreateSaleAsync(
                It.IsAny<CreateSaleRequest>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreateSaleRequest, Guid, Guid?, CancellationToken>((_, cid, uid, _) =>
            {
                capturedCompanyId = cid;
                capturedUserId = uid;
            })
            .ReturnsAsync(new Either<Error, SaleDto>.Right(new SaleDto
                { Id = Guid7TestValues.SaleA, CompanyId = JwtCompanyId }));

        var controller = BuildController(mockServiceManager.Object, JwtCompanyId, JwtUserId);
        var request = BuildCreateSaleRequest();

        _ = await controller.CreateSale(request, CancellationToken.None);

        capturedCompanyId.Should().Be(JwtCompanyId);
        capturedUserId.Should().Be(JwtUserId,
            "userId propagation is unchanged by the Guid 7 migration — only the tenant binding moved.");
    }
}