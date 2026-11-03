using FluentAssertions;
using Volterp.Domain.Exceptions;
using Xunit;

namespace Volterp.Tests.Exceptions;

public class ExceptionTests
{
    [Fact]
    public void InsufficientStockException_ShouldIncludeRequestedAndAvailable()
    {
        var ex = new InsufficientStockException(requested: 10, available: 3);

        ex.Requested.Should().Be(10);
        ex.Available.Should().Be(3);
        ex.Message.Should().Contain("10").And.Contain("3");
    }

    [Fact]
    public void InvalidEmailException_ShouldIncludeInvalidValue()
    {
        var ex = new InvalidEmailException("not-an-email");

        ex.InvalidValue.Should().Be("not-an-email");
        ex.Message.Should().Contain("not-an-email");
    }

    [Fact]
    public void InvalidRncException_ShouldIncludeInvalidValue()
    {
        var ex = new InvalidRncException("ABC-1234");

        ex.InvalidValue.Should().Be("ABC-1234");
        ex.Message.Should().Contain("ABC-1234");
    }

    [Fact]
    public void InvalidSaleStateTransitionException_ShouldIncludeFromAndTo()
    {
        var ex = new InvalidSaleStateTransitionException(fromStatus: "Pending", toStatus: "Cancelled");

        ex.FromStatus.Should().Be("Pending");
        ex.ToStatus.Should().Be("Cancelled");
        ex.Message.Should().Contain("Pending").And.Contain("Cancelled");
    }

    [Fact]
    public void InvalidRoleTransitionException_ShouldIncludeFromAndTo()
    {
        var ex = new InvalidRoleTransitionException(fromRole: "Ventas", toRole: "SuperAdmin");

        ex.FromRole.Should().Be("Ventas");
        ex.ToRole.Should().Be("SuperAdmin");
        ex.Message.Should().Contain("Ventas").And.Contain("SuperAdmin");
    }
}
