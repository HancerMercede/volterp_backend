using FluentAssertions;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;
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

    [Fact]
    public void Client_Activate_WhenAlreadyActive_IsIdempotent()
    {
        var client = new Client { IsActive = true };

        client.Activate();

        client.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Client_UpdateEmail_WithValidEmail_SetsEmailValue()
    {
        var client = new Client();
        var email = new Email("client@example.com");

        client.UpdateEmail(email);

        client.Email.Should().Be("client@example.com");
    }

    [Fact]
    public void Client_UpdatePhone_SetsPhone()
    {
        var client = new Client();

        client.UpdatePhone("555-1234");

        client.Phone.Should().Be("555-1234");
    }

    // ===== Supplier =====

    [Fact]
    public void Supplier_Activate_SetsIsActiveTrue()
    {
        var supplier = new Supplier { IsActive = false };

        supplier.Activate();

        supplier.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Supplier_Deactivate_SetsIsActiveFalse()
    {
        var supplier = new Supplier { IsActive = true };

        supplier.Deactivate();

        supplier.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Supplier_UpdateEmail_WithValidEmail_SetsEmailValue()
    {
        var supplier = new Supplier();
        var email = new Email("supplier@example.com");

        supplier.UpdateEmail(email);

        supplier.Email.Should().Be("supplier@example.com");
    }

    [Fact]
    public void Supplier_UpdatePhone_SetsPhone()
    {
        var supplier = new Supplier();

        supplier.UpdatePhone("555-5678");

        supplier.Phone.Should().Be("555-5678");
    }

    // ===== Category =====

    [Fact]
    public void Category_Activate_SetsIsActiveTrue()
    {
        var category = new Category { IsActive = false };

        category.Activate();

        category.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Category_Deactivate_SetsIsActiveFalse()
    {
        var category = new Category { IsActive = true };

        category.Deactivate();

        category.IsActive.Should().BeFalse();
    }

    // ===== Company =====

    [Fact]
    public void Company_Activate_SetsIsActiveTrue()
    {
        var company = new Company { IsActive = false };

        company.Activate();

        company.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Company_Deactivate_SetsIsActiveFalse()
    {
        var company = new Company { IsActive = true };

        company.Deactivate();

        company.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Company_UpdateTaxId_SetsTaxIdValue()
    {
        var company = new Company();
        var taxId = new TaxId("ABCD12345678");

        company.UpdateTaxId(taxId);

        company.TaxId.Should().Be("ABCD12345678");
    }

    [Fact]
    public void Company_UpdateEmail_SetsEmail()
    {
        var company = new Company();
        var email = new Email("company@example.com");

        company.UpdateEmail(email);

        company.Email.Should().Be("company@example.com");
    }

    [Fact]
    public void Company_UpdatePhone_SetsPhone()
    {
        var company = new Company();

        company.UpdatePhone("555-0000");

        company.Phone.Should().Be("555-0000");
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

    [Fact]
    public void Employee_UpdateEmail_SetsEmail()
    {
        var employee = new Employee();
        var email = new Email("employee@example.com");

        employee.UpdateEmail(email);

        employee.Email.Should().Be("employee@example.com");
    }

    [Fact]
    public void Employee_UpdateSalary_SetsSalary()
    {
        var employee = new Employee();

        employee.UpdateSalary(75000);

        employee.Salary.Should().Be(75000);
    }
}
