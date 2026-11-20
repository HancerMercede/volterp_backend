using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Api.Authorization;
using Volterp.Domain.Authorization;
using Volterp.Domain.Enums;
using Xunit;

namespace Volterp.Tests.Authorization;

// The policies of the API: one per permission of the catalog, and the
// permission of a caller is the union of the permissions of its roles.
public class PermissionPolicyTests
{
    [Fact]
    public void EveryPermissionOfTheCatalogHasItsPolicy()
    {
        var declared = PermissionPolicies.Names;
        var expected = Permissions.All.Select(PermissionPolicies.Name).ToList();

        declared.Should().BeEquivalentTo(expected,
            "the catalog is closed: a permission that is not declared cannot be asked for by an endpoint");
    }

    [Fact]
    public void TheNamesOfThePoliciesAreUnique()
    {
        PermissionPolicies.Names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ThePermissionsOfTheRolesAreTheUnionOfThem()
    {
        var permissions = RolePermissions.For([UserRole.Ventas, UserRole.Inventario]);

        permissions.Should().Contain(Permission.SalesRead);
        permissions.Should().Contain(Permission.InventoryRead);
        permissions.Should().NotContain(Permission.AccountingPost);
    }

    [Fact]
    public async Task TheCallerIsLetInWhenItHoldsTheRoleThatGrantsThePermission()
    {
        var authorization = BuildAuthorization();

        var result = await authorization.AuthorizeAsync(
            PrincipalOf(UserRole.Ventas), null, PermissionPolicies.SalesRead);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task TheCallerIsRefusedWhenNoRoleOfItGrantsThePermission()
    {
        var authorization = BuildAuthorization();

        var result = await authorization.AuthorizeAsync(
            PrincipalOf(UserRole.Ventas), null, PermissionPolicies.InventoryRead);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task TheCallerWithTwoRolesHasThePermissionsOfBoth()
    {
        var authorization = BuildAuthorization();

        var result = await authorization.AuthorizeAsync(
            PrincipalOf(UserRole.Ventas, UserRole.Inventario), null, PermissionPolicies.InventoryRead);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task TheCallerWithoutRolesHasNoPermission()
    {
        var authorization = BuildAuthorization();

        var result = await authorization.AuthorizeAsync(
            PrincipalOfClaims(), null, PermissionPolicies.SalesRead);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task TheClaimOfARoleNobodyKnowsGrantsNothing()
    {
        var authorization = BuildAuthorization();

        var result = await authorization.AuthorizeAsync(
            PrincipalOfClaims("Empleado"), null, PermissionPolicies.SalesRead);

        result.Succeeded.Should().BeFalse();
    }

    private static IAuthorizationService BuildAuthorization()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAuthorization(options => PermissionPolicies.Register(options));
        services.AddSingleton<IAuthorizationHandler, PermissionRequirementHandler>();

        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal PrincipalOf(params UserRole[] roles)
        => PrincipalOfClaims(roles.Select(role => role.ToString()).ToArray());

    private static ClaimsPrincipal PrincipalOfClaims(params string[] roles)
    {
        var claims = roles
            .Select(role => new Claim(ClaimTypes.Role, role))
            .ToList();

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", ClaimTypes.Name, ClaimTypes.Role));
    }
}
