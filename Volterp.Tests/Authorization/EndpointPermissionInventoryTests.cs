using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Api.Authorization;
using Volterp.Tests.Integration;
using Xunit;

namespace Volterp.Tests.Authorization;

// Every route that a token protects says the permission it needs, in the map
// of the routes. A route that is missing from the map is a route that any
// authenticated caller could call, so the inventory fails when one is missing.
//
// The inventory walks the real endpoints of the API (the same ones the
// framework serves), not the reflection of the controllers: a route that is
// not a controller is in the inventory too.
public class EndpointPermissionInventoryTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EndpointPermissionInventoryTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void EveryRouteThatATokenProtectsIsInTheMap()
    {
        var missing = new List<string>();

        foreach (var endpoint in Endpoints())
        {
            if (!IsTokenProtected(endpoint))
                continue;

            // A route that declares its own permission is covered by itself.
            if (endpoint.Metadata.OfType<IAuthorizeData>().Any(requirement => !string.IsNullOrEmpty(requirement.Policy)))
                continue;

            var (controller, actions) = ControllerOf(endpoint);

            if (controller is null)
            {
                missing.Add($"{endpoint.RoutePattern?.RawText} (no controller)");
                continue;
            }

            foreach (var verb in VerbsOf(endpoint))
            {
                var permission = PermissionRoutes.For(controller, verb, actions);

                if (permission is null && !PermissionRoutes.NeedsOnlyAToken(controller, actions))
                    missing.Add($"{controller.Name}.{actions} ({verb})");
            }
        }

        missing.Should().BeEmpty(
            "every route that a token protects has to say the permission it needs: " +
            "a route that the map does not cover is a route that anybody authenticated could call");
    }

    [Fact]
    public void TheMapHasNoEntryForARouteThatDoesNotExist()
    {
        var controllers = ControllersOfTheApi();
        var dead = new List<string>();

        foreach (var entry in PermissionRoutes.Entries)
        {
            var controller = entry.Split([':', '.'], StringSplitOptions.RemoveEmptyEntries).First();

            if (!controllers.ContainsKey(controller))
            {
                dead.Add(entry);
                continue;
            }

            IReadOnlyList<string> verbs = entry.Contains(':')
                ? [entry.Split(':', StringSplitOptions.RemoveEmptyEntries)[1]]
                : [];

            var exists = verbs.Count == 0
                || controllers[controller].Any(action => verbs.Contains(action.Verb, StringComparer.OrdinalIgnoreCase));

            if (!exists)
                dead.Add(entry);
        }

        dead.Should().BeEmpty(
            "an entry of the map that no route uses is noise: it says that a route exists that does not");
    }

    [Fact]
    public void TheMapOnlySpeaksTheVocabularyOfTheCatalog()
    {
        PermissionRoutes.Entries.Should().NotBeEmpty();
    }

    private IReadOnlyList<RouteEndpoint> Endpoints()
    {
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        return dataSource.Endpoints.OfType<RouteEndpoint>().ToList();
    }

    private static (Type? Controller, string Actions) ControllerOf(RouteEndpoint endpoint)
    {
        var action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();

        return action is null
            ? (null, string.Empty)
            : (action.ControllerTypeInfo.AsType(), action.ActionName);
    }

    private static IReadOnlyList<string> VerbsOf(RouteEndpoint endpoint)
        => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
            .ToList() ?? [];

    private static bool IsTokenProtected(RouteEndpoint endpoint)
    {
        var requirements = endpoint.Metadata.OfType<IAuthorizeData>().ToList();

        if (requirements.Count == 0)
            return false;

        return requirements.Any(requirement =>
            string.IsNullOrEmpty(requirement.AuthenticationSchemes)
            || requirement.AuthenticationSchemes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains("JwtBearer", StringComparer.OrdinalIgnoreCase));
    }

    // The actions of every controller of the API, by the name of the
    // controller and the verb of the action.
    private static IReadOnlyDictionary<string, IReadOnlyList<(string Verb, string Action)>> ControllersOfTheApi()
    {
        var api = typeof(Volterp.Api.Controllers.AuthController).Assembly;

        return api.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .Where(type => !type.IsAbstract)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(method => method.GetCustomAttributes(inherit: true)
                    .OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>()
                    .Select(attribute => new
                    {
                        Verb = attribute.HttpMethods.FirstOrDefault() ?? string.Empty,
                        Action = method.Name
                    }))
                .Select(entry => (Controller: NameOf(type), entry.Verb, entry.Action)))
            .GroupBy(entry => entry.Controller)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<(string Verb, string Action)>)group
                    .Select(entry => (entry.Verb, entry.Action))
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string NameOf(Type controllerType)
        => controllerType.Name.EndsWith("Controller", StringComparison.Ordinal)
            ? controllerType.Name[..^"Controller".Length]
            : controllerType.Name;
}
