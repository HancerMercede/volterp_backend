using EitherWay;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Volterp.Application.DTOs.UserDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Tools.Onboarding;
using Xunit;

namespace Volterp.Tests.Onboarding;

// The first tenant of a client is created by a command of the deployment: the
// company with the RNC it really has, and an administrator whose password only
// the client knows.
public class TenantOnboardingTests
{
    private static readonly TenantOnboardingRequest Request = new(
        Name: "Distribuidora del Caribe",
        TaxId: "101500018",
        LegalName: "Distribuidora del Caribe S.R.L.",
        Address: "Av. 27 de Febrero No. 42, Santo Domingo Este",
        Phone: "809-555-0101",
        Email: "contacto@distcaribe.do",
        AdminUsername: "admin",
        AdminEmail: "admin@distcaribe.do",
        AdminFullName: "Administrador");

    [Fact]
    public async Task TheCommandCreatesTheCompanyWithItsRncAndItsAdmin()
    {
        await using var fixture = new OnboardingFixture();
        await fixture.InitializeAsync();

        var outcome = Succeeded(await fixture.Onboarding.RunAsync(Request));

        outcome.CompanyAlreadyExisted.Should().BeFalse();
        outcome.TaxId.Should().Be("101500018", "the RNC of the company is the fiscal identity of the tenant");
        outcome.AdminPassword.Should().NotBeNullOrEmpty("the administrator has to be able to log in");

        var company = await fixture.Context.Companies.SingleAsync();
        company.TaxId.Should().Be("101500018");
        company.Name.Should().Be(Request.Name);
        company.LegalName.Should().Be(Request.LegalName);
        company.Address.Should().Be(Request.Address);

        var admin = await fixture.Context.Users.Include(candidate => candidate.Roles).SingleAsync();
        admin.Username.Should().Be(Request.AdminUsername);
        admin.Email.Should().Be(Request.AdminEmail);
        admin.Roles.Select(assignment => assignment.Role)
            .Should().ContainSingle().Which.Should().Be(UserRole.SuperAdmin);
        admin.CompanyId.Should().Be(company.Id);
        admin.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task TheCommandIsIdempotent()
    {
        await using var fixture = new OnboardingFixture();
        await fixture.InitializeAsync();

        await fixture.Onboarding.RunAsync(Request);

        var second = Succeeded(await fixture.Onboarding.RunAsync(Request));

        second.CompanyAlreadyExisted.Should().BeTrue(
            "the command run twice on the same RNC has to say that the tenant is already there");
        second.AdminPassword.Should().BeNull("the second run does not create an administrator");

        (await fixture.Context.Companies.CountAsync()).Should().Be(1);
        (await fixture.Context.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task TheCommandGivesTheAdministratorToACompanyThatHasNone()
    {
        await using var fixture = new OnboardingFixture();
        await fixture.InitializeAsync();

        await fixture.Context.Companies.AddAsync(new Company
        {
            Id = Guid.CreateVersion7(),
            Name = Request.Name,
            TaxId = Request.TaxId,
            Address = Request.Address,
            LegalName = Request.LegalName,
            Phone = Request.Phone,
            Email = Request.Email
        });

        await fixture.Context.SaveChangesAsync();

        var outcome = Succeeded(await fixture.Onboarding.RunAsync(Request));

        outcome.CompanyAlreadyExisted.Should().BeTrue(
            "the company was already there: the command does not create it again");
        outcome.AdminPassword.Should().NotBeNullOrEmpty(
            "a company without an administrator is a tenant nobody can administer: the command has to create the first one");

        var admin = await fixture.Context.Users.Include(candidate => candidate.Roles).SingleAsync();
        admin.Username.Should().Be(Request.AdminUsername);
        admin.Roles.Select(assignment => assignment.Role)
            .Should().ContainSingle().Which.Should().Be(UserRole.SuperAdmin);
        admin.CompanyId.Should().NotBe(Guid.Empty);

        (await fixture.Context.Companies.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task TheCommandRefusesAnEmailThatOnlyDiffersInTheCaseOfOneThatIsInUse()
    {
        await using var fixture = new OnboardingFixture();
        await fixture.InitializeAsync();

        await fixture.Onboarding.RunAsync(Request);

        var another = Request with
        {
            TaxId = "101500026",
            AdminUsername = "otro-admin",
            AdminEmail = Request.AdminEmail.ToUpperInvariant()
        };

        var failure = Failed(await fixture.Onboarding.RunAsync(another));

        failure.Should().Contain("email");

        (await fixture.Context.Users.CountAsync()).Should().Be(1,
            "the same address cannot pass in another case");
    }

    [Fact]
    public async Task TheCommandFailsClosedWithAnInvalidRnc()
    {
        await using var fixture = new OnboardingFixture();
        await fixture.InitializeAsync();

        var invalid = Request with { TaxId = "123456789" };

        var failure = Failed(await fixture.Onboarding.RunAsync(invalid));

        failure.Should().Contain("RNC");

        (await fixture.Context.Companies.AnyAsync(c => c.TaxId == invalid.TaxId)).Should().BeFalse(
            "a tenant with a fiscal identity that does not exist cannot be created");
        (await fixture.Context.Users.AnyAsync(u => u.Username == invalid.AdminUsername)).Should().BeFalse(
            "the administrator of a tenant that was not created cannot exist either");
    }

    [Fact]
    public async Task TheAdministratorCreatedCanAuthenticate()
    {
        await using var fixture = new OnboardingFixture();
        await fixture.InitializeAsync();

        var outcome = Succeeded(await fixture.Onboarding.RunAsync(Request));

        var authentication = await fixture.UserService.GetForAuthenticationAsync(Request.AdminUsername);

        var user = ((Either<Error, UserWithPasswordHashDto?>.Right)authentication).Value;

        user.Should().NotBeNull();
        user!.PasswordHash.Should().StartWith("$2",
            "the password has to travel hashed with the hasher of the repository, never in clear");
        user.PasswordHash.Should().NotBe(outcome.AdminPassword);
        fixture.Hasher.Verify(outcome.AdminPassword!, user.PasswordHash).Should().BeTrue(
            "the administrator has to be able to log in with the password that the command showed");
    }

    private static TenantOnboardingOutcome Succeeded(Either<Error, TenantOnboardingOutcome> result)
    {
        result.Should().BeOfType<Either<Error, TenantOnboardingOutcome>.Right>(
            "the command was expected to create the tenant");

        return ((Either<Error, TenantOnboardingOutcome>.Right)result).Value;
    }

    private static string Failed(Either<Error, TenantOnboardingOutcome> result)
    {
        result.Should().BeOfType<Either<Error, TenantOnboardingOutcome>.Left>(
            "the command was expected to refuse the tenant");

        return ((Either<Error, TenantOnboardingOutcome>.Left)result).Value.Message;
    }
}
