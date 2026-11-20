using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Volterp.Api.Authorization;
using Volterp.Api.Controllers;
using Xunit;

namespace Volterp.Tests.Authorization;

// Every route that a token protects says the permission it needs, in the map
// of the routes. A route that is missing from the map is a route that any
// authenticated caller could call, so the inventory fails when one is missing.
public class EndpointPermissionInventoryTests
{
    private static readonly Assembly Api = typeof(AuthController).Assembly;

    [Fact]
    public void EveryRouteThatATokenProtectsIsInTheMap()
    {
        var missing = new List<string>();

        foreach (var controller in Api.GetTypes()
                     .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
                     .Where(type => !type.IsAbstract))
        {
            var classAuthorization = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).FirstOrDefault();
            var classAnonymous = controller.GetCustomAttribute<AllowAnonymousAttribute>();

            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var verbs = VerbsOf(action);

                if (verbs.Count == 0)
                    continue;

                var actionAuthorization = action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).FirstOrDefault();
                var actionAnonymous = action.GetCustomAttribute<AllowAnonymousAttribute>();

                var effective = actionAuthorization ?? classAuthorization;

                if (effective is null)
                    continue;

                if (actionAnonymous is not null || classAnonymous is not null)
                    continue;

                if (!IsTokenScheme(effective))
                    continue;

                foreach (var verb in verbs)
                {
                    var permission = PermissionRoutes.For(controller, verb, action.Name);

                    if (permission is null && !PermissionRoutes.NeedsOnlyAToken(controller, action.Name))
                        missing.Add($"{controller.Name}.{action.Name} ({verb})");
                }
            }
        }

        missing.Should().BeEmpty(
            "every route that a token protects has to say the permission it needs: " +
            "a route that the map does not cover is a route that anybody authenticated could call");
    }

    [Fact]
    public void EveryPermissionOfTheMapIsOneOfTheCatalog()
    {
        // The map only speaks the vocabulary of the catalog: a permission that
        // does not exist cannot be granted by accident.
        PermissionRoutes.Entries.Should().NotBeEmpty();

        foreach (var entry in PermissionRoutes.Entries)
            entry.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void EveryControllerOfTheMapExists()
    {
        // A typo in the map would leave a route uncovered, and the inventory
        // above would fail with a message that does not point at the typo.
        var controllers = Api.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .Select(type => type.Name.EndsWith("Controller", StringComparison.Ordinal)
                ? type.Name[..^"Controller".Length]
                : type.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in PermissionRoutes.Entries)
        {
            var controller = entry.Split([':', '.'], StringSplitOptions.RemoveEmptyEntries).First();

            controllers.Should().Contain(controller,
                "the map names a controller that does not exist");
        }
    }

    private static IReadOnlyList<string> VerbsOf(MethodInfo action)
        => action.GetCustomAttributes(inherit: true)
            .OfType<HttpMethodAttribute>()
            .SelectMany(attribute => attribute.HttpMethods)
            .ToArray();

    // The routes that a third party enters with its API key are not the
    // user's: they do not answer to the matrix.
    private static bool IsTokenScheme(AuthorizeAttribute requirement)
        => string.IsNullOrEmpty(requirement.AuthenticationSchemes)
           || requirement.AuthenticationSchemes
               .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               .Contains("JwtBearer", StringComparer.OrdinalIgnoreCase);
}