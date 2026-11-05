using FluentAssertions;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Xunit;

namespace Volterp.Tests.Domain;

public class CodeFormatTests
{
    [Theory]
    [InlineData(CodeEntityType.Product, "Prod")]
    [InlineData(CodeEntityType.Category, "Cat")]
    [InlineData(CodeEntityType.Client, "Cli")]
    [InlineData(CodeEntityType.Supplier, "Prov")]
    [InlineData(CodeEntityType.Sale, "Vent")]
    [InlineData(CodeEntityType.Purchase, "Comp")]
    [InlineData(CodeEntityType.Employee, "Empl")]
    [InlineData(CodeEntityType.AccountingTransaction, "Asi")]
    [InlineData(CodeEntityType.User, "Usu")]
    public void Build_UsesThePrefixOfTheEntity(CodeEntityType entityType, string prefix)
    {
        CodeFormat.Build(entityType, 1).Should().Be($"{prefix}-001");
    }

    [Fact]
    public void Build_PadsTheValueToThreeDigits()
    {
        CodeFormat.Build(CodeEntityType.Sale, 42).Should().Be("Vent-042");
    }

    [Fact]
    public void Build_KeepsEveryDigitOnceTheValuePassesThreeDigits()
    {
        CodeFormat.Build(CodeEntityType.Sale, 1234).Should().Be("Vent-1234");
    }
}
