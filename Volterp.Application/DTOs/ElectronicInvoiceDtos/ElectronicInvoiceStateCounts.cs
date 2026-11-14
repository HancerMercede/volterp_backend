namespace Volterp.Application.DTOs.ElectronicInvoiceDtos;

// How many documents of the tenant are open in each state.
public record ElectronicInvoiceStateCounts
{
    public int PendingEmission { get; init; }
    public int InProcess { get; init; }
    public int Rejected { get; init; }
    public int RequiresReview { get; init; }

    public int Total => PendingEmission + InProcess + Rejected + RequiresReview;
}
