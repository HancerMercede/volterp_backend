using FluentAssertions;
using Xunit;

namespace Volterp.Tests.Seeding;

public class DeploymentCommandPreservationTests
{
    [Fact]
    public async Task TheTenantCommandStillRefusesToRunWithoutItsArguments()
    {
        // The name is qualified because the test host has a program of its
        // own in the global namespace.
        var exitCode = await Volterp.Tools.Program.Main(["create-tenant"]);

        exitCode.Should().Be(2);
    }
}
