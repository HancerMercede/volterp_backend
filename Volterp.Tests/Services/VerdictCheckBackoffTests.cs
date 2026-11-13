using FluentAssertions;
using Volterp.Domain.Policies;
using Xunit;

namespace Volterp.Tests.Services;

// The wait between two verdict checks: it grows with every check the document
// carries, it is dispersed so a batch of documents does not ask the DGII at
// the same instant, and it never passes the ceiling.
public class VerdictCheckBackoffTests
{
    private static readonly TimeSpan FirstWait = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Ceiling = TimeSpan.FromHours(4);

    [Fact]
    public void TheWaitGrowsWithEveryCheckTheDocumentCarries()
    {
        var backoff = Backoff(() => 0.5);

        backoff.DelayFor(0).Should().Be(FirstWait);
        backoff.DelayFor(1).Should().Be(FirstWait * 2);
        backoff.DelayFor(2).Should().Be(FirstWait * 4);
    }

    [Fact]
    public void TheWaitNeverPassesTheCeiling()
    {
        var backoff = Backoff(() => 0.5);

        backoff.DelayFor(50).Should().Be(Ceiling);
    }

    [Fact]
    public void TheDispersionMovesTheWaitInsideItsBand()
    {
        // The band is a fifth of the wait: the dispersion is what keeps a
        // batch of documents from asking the DGII at the same instant.
        Backoff(() => 0).DelayFor(0).Should().BeCloseTo(FirstWait * 0.8, TimeSpan.FromMilliseconds(1));
        Backoff(() => 1).DelayFor(0).Should().BeCloseTo(FirstWait * 1.2, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void TheDispersionNeverPushesTheWaitPastTheCeiling()
    {
        Backoff(() => 1).DelayFor(50).Should().Be(Ceiling);
    }

    [Fact]
    public void APolicyThatCannotWaitIsRejected()
    {
        var act = () => new VerdictCheckBackoff(TimeSpan.Zero, Ceiling, 0.2, () => 0.5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static VerdictCheckBackoff Backoff(Func<double> random)
        => new(FirstWait, Ceiling, 0.2, random);
}
