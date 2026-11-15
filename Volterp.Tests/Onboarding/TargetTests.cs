using FluentAssertions;
using Volterp.Tools;
using Xunit;

namespace Volterp.Tests.Onboarding;

// The command says out loud which database it is about to write to, before it
// writes: nobody should create a tenant in a database they did not mean.
public class TargetTests
{
    [Fact]
    public void TheTargetSaysTheHostAndTheDatabase()
    {
        var described = Target.Describe(
            "Host=localhost;Database=volterp_test;Username=volterp;Password=temppassword123;Port=5432");

        described.Should().Be("localhost/volterp_test");
    }
}
