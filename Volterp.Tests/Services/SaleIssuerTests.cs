using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs.SaleDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Enums;
using Xunit;

namespace Volterp.Tests.Services;

// The sale names the RNC that issues it. It has to be one of the issuers of
// the tenant: with the RNC of another taxpayer, the tenant would issue in
// its name.
public class SaleIssuerTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    [Fact]
    public async Task CreateSaleAsync_WithTheRncOfAnotherCompany_ReturnsLeft()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var issuers = new Mock<ICompanyIssuerRepository>();

        unitOfWork.Setup(u => u.CompanyIssuers).Returns(issuers.Object);

        issuers.Setup(i => i.ResolveIssuerTaxIdAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "The issuer RNC 101500018 is not one of the issuers of this company."));

        var service = new SaleService(
            unitOfWork.Object,
            Mock.Of<IElectronicInvoiceService>(),
            Mock.Of<INormalFiscalDocumentService>());

        var result = await service.CreateSaleAsync(Request(), CompanyId, null);

        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        ((Either<Error, SaleDto>.Left)result).Value.Message.Should().Contain("issuer");
    }

    private static CreateSaleRequest Request()
        => new(
            null,
            "Cliente",
            SaleStatus.Pending,
            0m,
            null,
            [
                new CreateSaleItemRequest(
                    Guid.NewGuid(), "Café molido", null, null, null, 1, 100m, 100m)
            ],
            IssuerTaxId: "101500018");
}
