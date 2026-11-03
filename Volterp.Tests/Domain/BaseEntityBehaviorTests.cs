using FluentAssertions;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Xunit;

namespace Volterp.Tests.Domain;

public class BaseEntityBehaviorTests
{
    // ===== Client =====

    [Fact]
    public void Client_Activate_SetsIsActiveTrue()
    {
        var client = new Client { IsActive = false };

        client.Activate();

        client.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Client_Deactivate_SetsIsActiveFalse()
    {
        var client = new Client { IsActive = true };

        client.Deactivate();

        client.IsActive.Should().BeFalse();
    }

    // ===== Company =====

    [Fact]
    public void Company_UpdatePhone_WithNull_SetsEmptyString()
    {
        var company = new Company();

        company.UpdatePhone(null);

        company.Phone.Should().Be(string.Empty);
    }

    // ===== Employee =====

    [Fact]
    public void Employee_Activate_SetsStatusToActive()
    {
        var employee = new Employee { Status = EntityStatus.Inactive };

        employee.Activate();

        employee.Status.Should().Be(EntityStatus.Active);
    }

    [Fact]
    public void Employee_Deactivate_SetsStatusToInactive()
    {
        var employee = new Employee { Status = EntityStatus.Active };

        employee.Deactivate();

        employee.Status.Should().Be(EntityStatus.Inactive);
    }
}
