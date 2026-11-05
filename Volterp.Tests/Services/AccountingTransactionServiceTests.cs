using EitherWay;
using FluentAssertions;
using Moq;
using Volterp.Application.DTOs;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Services;

public class AccountingTransactionServiceTests
{
    private static readonly Guid TransactionId = Guid.Parse("0192b9c7-b6a3-7def-8c5e-1f2a3b4c5d79");

    private static AccountingTransactionDto Request(string? code = null, string referenceNumber = "REF-01")
        => new(
            Id: Guid.NewGuid(),
            TransactionType: TransactionType.Debit,
            Amount: 1000m,
            Description: "Payroll payment",
            ReferenceNumber: referenceNumber,
            TransactionDate: new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Category: "Payroll",
            Notes: null,
            Status: EntityStatus.Active,
            Code: code);

    private static AccountingTransaction ExistingTransaction(string code)
        => new()
        {
            Id = TransactionId,
            CompanyId = Guid7TestValues.CompanyA,
            Code = code,
            TransactionType = TransactionType.Debit,
            Amount = 1000m,
            Description = "Payroll payment",
            ReferenceNumber = "REF-01",
            TransactionDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Category = "Payroll",
            Status = EntityStatus.Active
        };

    // ─────── Friendly codes (feature friendly-ids, T3d) ───────

    [Fact]
    public async Task CreateAsync_WithoutCode_UsesTheGeneratedCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockTransactions = new Mock<IAccountingTransactionRepository>();
        var mockCodes = new Mock<ICodeGenerator>();

        mockCodes.Setup(c => c.NextAsync(Guid7TestValues.CompanyA, CodeEntityType.AccountingTransaction, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Asi-001");
        mockTransactions.Setup(r => r.AddTransactionAsync(It.IsAny<AccountingTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountingTransaction t, CancellationToken ct) => t);
        mockUnitOfWork.Setup(u => u.AccountingTransactions).Returns(mockTransactions.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new AccountingTransactionService(mockUnitOfWork.Object);

        // ACT
        var result = await service.CreateAsync(Request(), Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, AccountingTransactionDto>.Right>();
        var dto = ((Either<Error, AccountingTransactionDto>.Right)result).Value;
        dto.Code.Should().Be("Asi-001");
        mockCodes.Verify(c => c.NextAsync(Guid7TestValues.CompanyA, CodeEntityType.AccountingTransaction, It.IsAny<CancellationToken>()), Times.Once);
        mockTransactions.Verify(r => r.AddTransactionAsync(It.Is<AccountingTransaction>(t => t.Code == "Asi-001"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithUserCode_KeepsTheUserCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockTransactions = new Mock<IAccountingTransactionRepository>();
        var mockCodes = new Mock<ICodeGenerator>();

        AccountingTransaction? persisted = null;
        mockTransactions.Setup(r => r.ExistsCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockTransactions.Setup(r => r.AddTransactionAsync(It.IsAny<AccountingTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<AccountingTransaction, CancellationToken>((t, _) => persisted = t)
            .ReturnsAsync((AccountingTransaction t, CancellationToken ct) => t);
        mockUnitOfWork.Setup(u => u.AccountingTransactions).Returns(mockTransactions.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new AccountingTransactionService(mockUnitOfWork.Object);

        // ACT — the code arrives padded, the service trims it
        var result = await service.CreateAsync(Request("  ASI-42  "), Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, AccountingTransactionDto>.Right>();
        var dto = ((Either<Error, AccountingTransactionDto>.Right)result).Value;
        dto.Code.Should().Be("ASI-42");
        persisted.Should().NotBeNull();
        persisted!.Code.Should().Be("ASI-42");
        mockCodes.Verify(c => c.NextAsync(It.IsAny<Guid>(), It.IsAny<CodeEntityType>(), It.IsAny<CancellationToken>()), Times.Never);
        // The uniqueness lookup is scoped to the company of the caller.
        mockTransactions.Verify(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "ASI-42", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateCode_ReturnsCodeAlreadyInUseError()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockTransactions = new Mock<IAccountingTransactionRepository>();
        var mockCodes = new Mock<ICodeGenerator>();

        mockTransactions.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "ASI-42", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockUnitOfWork.Setup(u => u.AccountingTransactions).Returns(mockTransactions.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);

        var service = new AccountingTransactionService(mockUnitOfWork.Object);

        // ACT
        var result = await service.CreateAsync(Request("ASI-42"), Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, AccountingTransactionDto>.Left>();
        var error = ((Either<Error, AccountingTransactionDto>.Left)result).Value;
        error.Message.Should().Be("The code is already in use.");
        mockTransactions.Verify(r => r.AddTransactionAsync(It.IsAny<AccountingTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        mockUnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        mockCodes.Verify(c => c.NextAsync(It.IsAny<Guid>(), It.IsAny<CodeEntityType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithoutCode_KeepsTheCurrentCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockTransactions = new Mock<IAccountingTransactionRepository>();

        var transaction = ExistingTransaction("Asi-007");

        mockTransactions.Setup(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        mockTransactions.Setup(r => r.UpdateTransactionAsync(It.IsAny<AccountingTransaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.AccountingTransactions).Returns(mockTransactions.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new AccountingTransactionService(mockUnitOfWork.Object);

        // ACT
        var result = await service.UpdateAsync(TransactionId, Guid7TestValues.CompanyA, Request(), Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, AccountingTransactionDto>.Right>();
        var dto = ((Either<Error, AccountingTransactionDto>.Right)result).Value;
        dto.Code.Should().Be("Asi-007");
        transaction.Code.Should().Be("Asi-007");
        mockTransactions.Verify(r => r.ExistsCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        mockTransactions.Verify(r => r.UpdateTransactionAsync(transaction, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithNewCode_AssignsTheNewCode()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockTransactions = new Mock<IAccountingTransactionRepository>();

        var transaction = ExistingTransaction("Asi-007");

        mockTransactions.Setup(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        mockTransactions.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "ASI-99", TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockTransactions.Setup(r => r.UpdateTransactionAsync(It.IsAny<AccountingTransaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.AccountingTransactions).Returns(mockTransactions.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new AccountingTransactionService(mockUnitOfWork.Object);

        // ACT
        var result = await service.UpdateAsync(TransactionId, Guid7TestValues.CompanyA, Request("  ASI-99  "), Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, AccountingTransactionDto>.Right>();
        var dto = ((Either<Error, AccountingTransactionDto>.Right)result).Value;
        dto.Code.Should().Be("ASI-99");
        transaction.Code.Should().Be("ASI-99");
        // The row being updated is excluded from the uniqueness lookup.
        mockTransactions.Verify(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "ASI-99", TransactionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateCode_ReturnsCodeAlreadyInUseError()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockTransactions = new Mock<IAccountingTransactionRepository>();

        var transaction = ExistingTransaction("Asi-007");

        mockTransactions.Setup(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        mockTransactions.Setup(r => r.ExistsCodeAsync(Guid7TestValues.CompanyA, "ASI-42", TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockUnitOfWork.Setup(u => u.AccountingTransactions).Returns(mockTransactions.Object);

        var service = new AccountingTransactionService(mockUnitOfWork.Object);

        // ACT
        var result = await service.UpdateAsync(TransactionId, Guid7TestValues.CompanyA, Request("ASI-42"), Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, AccountingTransactionDto>.Left>();
        var error = ((Either<Error, AccountingTransactionDto>.Left)result).Value;
        error.Message.Should().Be("The code is already in use.");
        mockTransactions.Verify(r => r.UpdateTransactionAsync(It.IsAny<AccountingTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        mockUnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        transaction.Code.Should().Be("Asi-007", "a rejected update must not change the stored code.");
    }

    // ─────── Pre-existing behaviour (regression cover) ───────

    [Fact]
    public async Task CreateAsync_KeepsTheReferenceNumberAsTheUserWroteIt()
    {
        // ARRANGE — the ReferenceNumber is the user's business reference, not the code (S4)
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockTransactions = new Mock<IAccountingTransactionRepository>();
        var mockCodes = new Mock<ICodeGenerator>();

        mockCodes.Setup(c => c.NextAsync(It.IsAny<Guid>(), It.IsAny<CodeEntityType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Asi-001");
        mockTransactions.Setup(r => r.AddTransactionAsync(It.IsAny<AccountingTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountingTransaction t, CancellationToken ct) => t);
        mockUnitOfWork.Setup(u => u.AccountingTransactions).Returns(mockTransactions.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);
        mockUnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new AccountingTransactionService(mockUnitOfWork.Object);

        // ACT
        var result = await service.CreateAsync(Request(referenceNumber: "  FAC-0001-99  "), Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, AccountingTransactionDto>.Right>();
        var dto = ((Either<Error, AccountingTransactionDto>.Right)result).Value;
        dto.ReferenceNumber.Should().Be("  FAC-0001-99  ");
        dto.Code.Should().Be("Asi-001");
        mockTransactions.Verify(
            r => r.AddTransactionAsync(It.Is<AccountingTransaction>(t => t.ReferenceNumber == "  FAC-0001-99  "), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithoutDescription_ReturnsTheValidationError()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockTransactions = new Mock<IAccountingTransactionRepository>();
        var mockCodes = new Mock<ICodeGenerator>();

        mockUnitOfWork.Setup(u => u.AccountingTransactions).Returns(mockTransactions.Object);
        mockUnitOfWork.Setup(u => u.Codes).Returns(mockCodes.Object);

        var service = new AccountingTransactionService(mockUnitOfWork.Object);
        var request = Request() with { Description = "  " };

        // ACT
        var result = await service.CreateAsync(request, Guid7TestValues.CompanyA, Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, AccountingTransactionDto>.Left>();
        var error = ((Either<Error, AccountingTransactionDto>.Left)result).Value;
        error.Message.Should().Be("The description is required.");
        mockTransactions.Verify(r => r.AddTransactionAsync(It.IsAny<AccountingTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenTransactionNotFound_ReturnsTheNotFoundError()
    {
        // ARRANGE
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockTransactions = new Mock<IAccountingTransactionRepository>();

        mockTransactions.Setup(r => r.GetByIdAsync(TransactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountingTransaction?)null);
        mockUnitOfWork.Setup(u => u.AccountingTransactions).Returns(mockTransactions.Object);

        var service = new AccountingTransactionService(mockUnitOfWork.Object);

        // ACT
        var result = await service.UpdateAsync(TransactionId, Guid7TestValues.CompanyA, Request(), Guid7TestValues.UserA);

        // ASSERT
        result.Should().BeOfType<Either<Error, AccountingTransactionDto>.Left>();
        var error = ((Either<Error, AccountingTransactionDto>.Left)result).Value;
        error.Message.Should().Be("Accounting transaction not found.");
    }
}
