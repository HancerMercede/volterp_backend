using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Volterp.Api.Controllers;
using Xunit;

namespace Volterp.Tests.Controllers;

public class SalesControllerAuthorizationTests
{
    [Fact]
    public void SalesController_IsDecoratedWithAuthorizeAttribute()
    {
        var authorizeAttribute = typeof(SalesController)
            .GetCustomAttribute<AuthorizeAttribute>(inherit: true);

        authorizeAttribute.Should().NotBeNull(
            "SalesController must require authentication at the class level, matching the sibling controllers");
    }

    [Fact]
    public void SalesController_NoPublicActionAllowsAnonymous()
    {
        var anonymousActions = typeof(SalesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null)
            .Select(m => m.Name)
            .ToArray();

        anonymousActions.Should().BeEmpty(
            "no public action on SalesController may opt out of the class-level [Authorize] requirement");
    }
}
