using FluentAssertions;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests;

// The taxonomy of the fiscal state: the four states that are still open and
// the verdicts that close the document. The count and the list of the
// surface are two uses of this classification, so it is pinned here.
public class EcfFiscalStateTests
{
    [Theory]
    [InlineData(EcfStatus.Signed, false, EcfFiscalState.PendingEmission)]
    [InlineData(EcfStatus.Sent, false, EcfFiscalState.InProcess)]
    [InlineData(EcfStatus.InProcess, false, EcfFiscalState.InProcess)]
    [InlineData(EcfStatus.Rejected, false, EcfFiscalState.Rejected)]
    [InlineData(EcfStatus.Draft, true, EcfFiscalState.RequiresReview)]
    public void Classify_AnOpenDocument_HasTheStateTheTenantSees(
        EcfStatus status, bool requiresReview, EcfFiscalState expected)
    {
        EcfFiscalStateClassifier.Classify(status, requiresReview).Should().Be(expected);
    }

    [Theory]
    [InlineData(EcfStatus.Accepted)]
    [InlineData(EcfStatus.AcceptedConditionally)]
    [InlineData(EcfStatus.Annulled)]
    public void Classify_AClosedDocument_HasNoState(EcfStatus status)
    {
        EcfFiscalStateClassifier.Classify(status, false).Should().BeNull(
            "a closed document has nothing left for the tenant to do");
    }

    [Fact]
    public void Classify_ADraftThatDoesNotRequireReview_IsTreatedAsRequiringReview()
    {
        EcfFiscalStateClassifier.Classify(EcfStatus.Draft, false).Should().Be(
            EcfFiscalState.RequiresReview,
            "a draft nobody has to look at should not exist: it fails closed into the review");
    }

    [Fact]
    public void Classify_ASignedDocumentThatRequiresReview_IsNotPendingEmission()
    {
        EcfFiscalStateClassifier.Classify(EcfStatus.Signed, true).Should().Be(
            EcfFiscalState.RequiresReview,
            "a document out of the automatic queue waits for a human, not for the queue");
    }
}
