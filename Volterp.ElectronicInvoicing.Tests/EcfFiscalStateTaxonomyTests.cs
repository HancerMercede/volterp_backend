using FluentAssertions;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests;

// The taxonomy of the fiscal state, pinned from the outside: the verdicts
// that close a document are written once and the four states the tenant sees
// are the only ones there are.
//
// A new verdict of the DGII falls here: the test that says the closed set is
// exactly the three terminal verdicts fails, so the new one cannot enter the
// surface without somebody deciding what it means.
public class EcfFiscalStateTaxonomyTests
{
    [Fact]
    public void TheClosedSetIsExactlyTheThreeTerminalVerdictsOfTheDgii()
    {
        EcfFiscalStateClassifier.ClosedStatuses.Should().Equal(
            new[] { EcfStatus.Accepted, EcfStatus.AcceptedConditionally, EcfStatus.Annulled },
            "a fourth terminal verdict of the DGII has to enter here on purpose");
    }

    [Fact]
    public void TheEnumHasExactlyTheFourOpenStates()
    {
        Enum.GetValues<EcfFiscalState>().Should().Equal(
            EcfFiscalState.PendingEmission,
            EcfFiscalState.InProcess,
            EcfFiscalState.Rejected,
            EcfFiscalState.RequiresReview);
    }

    [Fact]
    public void EveryVerdictIsClassifiedOrClosedAndNeverBoth()
    {
        foreach (var status in Enum.GetValues<EcfStatus>())
        {
            var state = EcfFiscalStateClassifier.Classify(status, false);
            var closed = EcfFiscalStateClassifier.ClosedStatuses.Contains(status);

            if (closed)
                state.Should().BeNull(
                    $"a document closed by {status} has nothing left for the tenant to do");
            else
                state.Should().NotBeNull(
                    $"a document in {status} is still open: it is in one of the four states");
        }
    }

    [Fact]
    public void ARejectionThatAlsoRequiresReviewIsARejection()
    {
        // The nullity of the document dominates what the review flag says:
        // the tenant has to read what the DGII answered.
        EcfFiscalStateClassifier.Classify(EcfStatus.Rejected, true).Should().Be(
            EcfFiscalState.Rejected);
    }
}
