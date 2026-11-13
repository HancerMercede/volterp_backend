using FluentAssertions;
using Volterp.Domain.Entities;
using Volterp.Domain.Exceptions;
using Volterp.ElectronicInvoicing;
using Xunit;

namespace Volterp.Tests.Services;

// The appointment of the next verdict check: it belongs to the document, and
// a document whose verdict is already final is not asked again.
public class ElectronicInvoiceSchedulingTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ScheduleNextCheck_KeepsTheAppointmentAndCountsTheCheck()
    {
        var invoice = InProcess();

        invoice.ScheduleNextCheck(Now.AddMinutes(2));

        invoice.NextCheckAt.Should().Be(Now.AddMinutes(2));
        invoice.StatusCheckCount.Should().Be(1);
    }

    [Theory]
    [InlineData(EcfStatus.Accepted)]
    [InlineData(EcfStatus.AcceptedConditionally)]
    [InlineData(EcfStatus.Rejected)]
    [InlineData(EcfStatus.Annulled)]
    public void ScheduleNextCheck_ThrowsWhenTheVerdictIsAlreadyFinal(EcfStatus status)
    {
        var invoice = Final(status);

        var act = () => invoice.ScheduleNextCheck(Now.AddMinutes(2));

        act.Should().Throw<InvalidElectronicInvoiceStateTransitionException>();
        invoice.NextCheckAt.Should().BeNull();
        invoice.StatusCheckCount.Should().Be(0);
    }

    [Fact]
    public void AFinalVerdictTakesTheAppointmentAway()
    {
        var invoice = InProcess();
        invoice.ScheduleNextCheck(Now.AddMinutes(2));

        invoice.Accept();

        invoice.NextCheckAt.Should().BeNull("a final verdict is not asked again");
    }

    private static ElectronicInvoice InProcess()
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
        invoice.MarkSent("track-1", Now);
        invoice.MarkInProcess();

        return invoice;
    }

    private static ElectronicInvoice Final(EcfStatus status)
    {
        var invoice = InProcess();

        switch (status)
        {
            case EcfStatus.Accepted:
                invoice.Accept();
                break;

            case EcfStatus.AcceptedConditionally:
                invoice.AcceptConditionally("El nombre del cliente no coincide con el RNC.");
                break;

            case EcfStatus.Rejected:
                invoice.Reject("El RNC del receptor no existe.");
                break;

            default:
                invoice.Accept();
                invoice.Annul();
                break;
        }

        return invoice;
    }
}
