using FluentAssertions;
using Volterp.Domain.Entities;
using Volterp.Domain.Exceptions;
using Volterp.ElectronicInvoicing;
using Xunit;

namespace Volterp.Tests.Domain;

public class ElectronicInvoiceBehaviorTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Sign_MovesTheInvoiceToSignedAndKeepsTheSignedXml()
    {
        var invoice = CreateInvoice();

        invoice.Sign("<e-CF />", Now);

        invoice.Status.Should().Be(EcfStatus.Signed);
        invoice.SignedXml.Should().Be("<e-CF />");
        invoice.SignedAt.Should().Be(Now);
    }

    [Fact]
    public void Sign_ThrowsWhenTheInvoiceIsNotDraft()
    {
        var invoice = CreateInvoice();
        invoice.Sign("<e-CF />", Now);

        var act = () => invoice.Sign("<e-CF />", Now);

        act.Should().Throw<InvalidElectronicInvoiceStateTransitionException>();
    }

    [Fact]
    public void Sign_ThrowsWhenTheSignedXmlIsEmpty()
    {
        var invoice = CreateInvoice();

        var act = () => invoice.Sign("  ", Now);

        act.Should().Throw<ArgumentException>();
        invoice.Status.Should().Be(EcfStatus.Draft);
    }

    [Fact]
    public void SignAfterReview_SignsADocumentWaitingForARetry()
    {
        var invoice = CreateInvoice();
        invoice.RequiresReview = true;

        invoice.SignAfterReview("<e-CF />", Now);

        invoice.Status.Should().Be(EcfStatus.Signed);
        invoice.SignedXml.Should().Be("<e-CF />");
        invoice.SignedAt.Should().Be(Now);
        invoice.RequiresReview.Should().BeFalse();
    }

    [Fact]
    public void SignAfterReview_RejectsADocumentThatDoesNotWaitForARetry()
    {
        var invoice = CreateInvoice();

        var act = () => invoice.SignAfterReview("<e-CF />", Now);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("The document does not require review.");
        invoice.Status.Should().Be(EcfStatus.Draft);
        invoice.SignedXml.Should().BeEmpty();
        invoice.SignedAt.Should().BeNull();
        invoice.RequiresReview.Should().BeFalse();
    }

    [Fact]
    public void SignAfterReview_RejectsANonDraftDocumentDuringARetry()
    {
        var invoice = CreateInvoice();
        invoice.RequiresReview = true;
        invoice.Status = EcfStatus.Accepted;

        var act = () => invoice.SignAfterReview("<e-CF />", Now);

        act.Should().Throw<InvalidElectronicInvoiceStateTransitionException>();
        invoice.Status.Should().Be(EcfStatus.Accepted);
        invoice.SignedXml.Should().BeEmpty();
        invoice.SignedAt.Should().BeNull();
        invoice.RequiresReview.Should().BeTrue();
    }

    [Fact]
    public void SequenceOf_ReadsBackThePositionUsedByARetry()
    {
        var encf = ElectronicInvoiceSequence.BuildEncf(EcfDocumentType.Consumer, 42);

        ElectronicInvoiceSequence.SequenceOf(encf).Should().Be(42);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("E32000000001")]
    [InlineData("E32000000000X")]
    public void SequenceOf_RejectsAMalformedEncfForARetry(string? encf)
    {
        var act = () => ElectronicInvoiceSequence.SequenceOf(encf!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkSent_MovesTheInvoiceToSentAndKeepsTheTrackId()
    {
        var invoice = SignedInvoice();

        invoice.MarkSent("TRACK-1", Now);

        invoice.Status.Should().Be(EcfStatus.Sent);
        invoice.TrackId.Should().Be("TRACK-1");
        invoice.SentAt.Should().Be(Now);
    }

    [Fact]
    public void MarkSent_ThrowsWhenTheInvoiceIsNotSigned()
    {
        var invoice = CreateInvoice();

        var act = () => invoice.MarkSent("TRACK-1", Now);

        act.Should().Throw<InvalidElectronicInvoiceStateTransitionException>();
    }

    [Fact]
    public void MarkInProcess_MovesTheInvoiceToInProcess()
    {
        var invoice = SentInvoice();

        invoice.MarkInProcess();

        invoice.Status.Should().Be(EcfStatus.InProcess);
    }

    [Fact]
    public void MarkInProcess_ThrowsWhenTheInvoiceWasNotSent()
    {
        var invoice = SignedInvoice();

        var act = () => invoice.MarkInProcess();

        act.Should().Throw<InvalidElectronicInvoiceStateTransitionException>();
    }

    [Fact]
    public void Accept_MovesTheInvoiceToAccepted()
    {
        var invoice = SentInvoice();

        invoice.Accept();

        invoice.Status.Should().Be(EcfStatus.Accepted);
    }

    [Fact]
    public void Accept_ThrowsWhenTheInvoiceWasNotSent()
    {
        var invoice = CreateInvoice();

        var act = () => invoice.Accept();

        act.Should().Throw<InvalidElectronicInvoiceStateTransitionException>();
    }

    [Fact]
    public void TheVerdictCanArriveAfterTheInvoiceIsInProcess()
    {
        var invoice = SentInvoice();
        invoice.MarkInProcess();

        invoice.Accept();

        invoice.Status.Should().Be(EcfStatus.Accepted);
    }

    [Fact]
    public void AcceptConditionally_KeepsTheObservation()
    {
        var invoice = SentInvoice();

        invoice.AcceptConditionally("El nombre del cliente no coincide con el RNC.");

        invoice.Status.Should().Be(EcfStatus.AcceptedConditionally);
        invoice.VerdictNote.Should().Be("El nombre del cliente no coincide con el RNC.");
    }

    [Fact]
    public void AcceptConditionally_ThrowsWithoutAnObservation()
    {
        var invoice = SentInvoice();

        var act = () => invoice.AcceptConditionally(" ");

        act.Should().Throw<ArgumentException>();
        invoice.Status.Should().Be(EcfStatus.Sent);
    }

    [Fact]
    public void Reject_KeepsTheReason()
    {
        var invoice = SentInvoice();

        invoice.Reject("El RNC del receptor no existe.");

        invoice.Status.Should().Be(EcfStatus.Rejected);
        invoice.VerdictNote.Should().Be("El RNC del receptor no existe.");
    }

    [Fact]
    public void Reject_ThrowsWithoutAReason()
    {
        var invoice = SentInvoice();

        var act = () => invoice.Reject("");

        act.Should().Throw<ArgumentException>();
        invoice.Status.Should().Be(EcfStatus.Sent);
    }

    [Fact]
    public void Annul_MovesAnAcceptedInvoiceToAnnulled()
    {
        var invoice = SentInvoice();
        invoice.Accept();

        invoice.Annul();

        invoice.Status.Should().Be(EcfStatus.Annulled);
    }

    [Fact]
    public void Annul_ThrowsWhenTheInvoiceWasNotAccepted()
    {
        var invoice = SentInvoice();

        var act = () => invoice.Annul();

        act.Should().Throw<InvalidElectronicInvoiceStateTransitionException>();
    }

    [Fact]
    public void ARejectedInvoiceCannotBeAnnulled()
    {
        var invoice = SentInvoice();
        invoice.Reject("El RNC del receptor no existe.");

        var act = () => invoice.Annul();

        act.Should().Throw<InvalidElectronicInvoiceStateTransitionException>();
    }

    [Theory]
    [InlineData(EcfStatus.Accepted, true)]
    [InlineData(EcfStatus.AcceptedConditionally, true)]
    [InlineData(EcfStatus.Rejected, false)]
    [InlineData(EcfStatus.Sent, false)]
    [InlineData(EcfStatus.Draft, false)]
    public void IsIssued_IsTrueOnlyWhenTheDgiiAcceptedIt(EcfStatus status, bool expected)
    {
        var invoice = CreateInvoice();
        invoice.Status = status;

        invoice.IsIssued.Should().Be(expected);
    }

    private static ElectronicInvoice CreateInvoice() => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = Guid.NewGuid(),
        SaleId = Guid.NewGuid(),
        ENCF = "E310000000001",
        DocumentType = EcfDocumentType.FiscalCredit,
        IssuerTaxId = "101000001",
        ReceiverTaxId = "130000001",
        Subtotal = 100m,
        Itbis = 18m,
        Total = 118m
    };

    private static ElectronicInvoice SignedInvoice()
    {
        var invoice = CreateInvoice();
        invoice.Sign("<e-CF />", Now);
        return invoice;
    }

    private static ElectronicInvoice SentInvoice()
    {
        var invoice = SignedInvoice();
        invoice.MarkSent("TRACK-1", Now);
        return invoice;
    }
}
