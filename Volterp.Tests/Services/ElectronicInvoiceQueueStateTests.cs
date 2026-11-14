using FluentAssertions;
using Volterp.Domain.Entities;
using Volterp.Domain.Exceptions;
using Volterp.ElectronicInvoicing;
using Xunit;

namespace Volterp.Tests.Services;

// The state of a document in the durable queue: the hold of a worker, the
// appointment of the next attempt and the flag that takes a document out of
// the automatic queue.
public class ElectronicInvoiceQueueStateTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TheDocumentThatWasReceivedGetsTheAppointmentOfItsFirstVerdictCheck()
    {
        var invoice = Signed();

        invoice.MarkSent("track-1", Now);

        // The document is with the DGII, but nobody asked for its verdict
        // yet: the appointment is what keeps it from being forgotten.
        invoice.Status.Should().Be(EcfStatus.Sent);
        invoice.NextCheckAt.Should().Be(Now);
    }

    [Fact]
    public void TheDocumentTheDgiiRefusedAtReceptionIsRejected()
    {
        var invoice = Signed();

        invoice.Reject("El archivo no cumple con el formato");

        // The rejection of the DGII is final: the document does not wait for
        // another attempt.
        invoice.Status.Should().Be(EcfStatus.Rejected);
        invoice.VerdictNote.Should().Be("El archivo no cumple con el formato");
        invoice.NextCheckAt.Should().BeNull();
    }

    [Fact]
    public void AFinalVerdictIsNotAskedAgain()
    {
        var invoice = Signed();
        invoice.MarkSent("track-1", Now);
        invoice.Accept();

        var act = () => invoice.ScheduleNextCheck(Now.AddMinutes(2));

        act.Should().Throw<InvalidElectronicInvoiceStateTransitionException>();
    }

    [Fact]
    public void AReceivedDocumentWaitsForItsVerdictAgain()
    {
        var invoice = Signed();
        invoice.MarkSent("track-1", Now);

        invoice.ScheduleNextCheck(Now.AddMinutes(2));

        invoice.NextCheckAt.Should().Be(Now.AddMinutes(2));
        invoice.StatusCheckCount.Should().Be(1);
    }

    [Fact]
    public void TheNextAttemptIsScheduledForTheDocumentThatIsStillWaiting()
    {
        var invoice = Signed();

        invoice.ScheduleNextAttempt(Now.AddMinutes(2));

        invoice.NextAttemptAt.Should().Be(Now.AddMinutes(2));
        invoice.Status.Should().Be(EcfStatus.Signed);
    }

    [Fact]
    public void ADocumentThatAlreadyReachedTheDgiiDoesNotWaitForAnotherAttempt()
    {
        var invoice = Signed();
        invoice.MarkSent("track-1", Now);

        var act = () => invoice.ScheduleNextAttempt(Now.AddMinutes(2));

        act.Should().Throw<InvalidElectronicInvoiceStateTransitionException>();
    }

    [Fact]
    public void TheHoldOfTheWorkerEndsWithTheWork()
    {
        var invoice = Signed();
        invoice.LeaseOwner = "worker-1";
        invoice.LeaseUntil = Now.AddMinutes(15);

        invoice.ReleaseLease();

        invoice.LeaseOwner.Should().BeNull();
        invoice.LeaseUntil.Should().BeNull();
    }

    [Fact]
    public void TheDocumentFlaggedForReviewKeepsTheReason()
    {
        var invoice = Signed();

        invoice.MarkForReview("The e-CF passed the 72-hour window of the contingency instructivo.");

        invoice.RequiresReview.Should().BeTrue();
        invoice.VerdictNote.Should().Be("The e-CF passed the 72-hour window of the contingency instructivo.");
    }

    private static ElectronicInvoice Signed()
    {
        var invoice = new ElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = Guid.CreateVersion7(),
            SaleId = Guid.CreateVersion7(),
            ENCF = "E320000000001",
            DocumentType = EcfDocumentType.Consumer,
            IssuerTaxId = "10100000101"
        };

        invoice.Sign("<ECF />", Now);

        return invoice;
    }
}
