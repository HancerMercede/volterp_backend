using FluentAssertions;
using Volterp.Domain.Policies;
using Xunit;

namespace Volterp.Tests.Services;

// The windows of the "Instructivo de Contingencia de Facturación
// Electrónica" of the DGII: the norm fixes them, not the code.
public class ContingencyWindowTests
{
    private static readonly DateTime Issued = new(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TheDocumentHasToReachTheDgiiWithinSeventyTwoHours()
    {
        var window = new ContingencyWindow();

        window.DeadlineToSend(Issued).Should().Be(Issued.AddHours(72));
    }

    [Fact]
    public void TheContingencyCannotLastMoreThanFifteenDays()
    {
        var window = new ContingencyWindow();

        window.EndOfTheContingency(Issued).Should().Be(Issued.AddDays(15));
    }

    [Fact]
    public void TheReplacementsAreSentWithinThirtyDaysOfTheEndOfTheContingency()
    {
        var window = new ContingencyWindow();

        window.DeadlineToReplace(Issued).Should().Be(Issued.AddDays(30));
    }

    [Fact]
    public void ADocumentThatWaitedLongerThanTheWindowIsNotSentAgain()
    {
        var window = new ContingencyWindow();

        window.PassedTheWindow(Issued, Issued.AddHours(71)).Should().BeFalse();
        window.PassedTheWindow(Issued, Issued.AddHours(73)).Should().BeTrue();
    }

    [Fact]
    public void TheQueueOnlyLooksAtTheDocumentsInsideTheCeiling()
    {
        var window = new ContingencyWindow();
        var now = Issued.AddDays(20);

        window.QueueCutoff(now).Should().Be(now.AddDays(-15));
    }
}
