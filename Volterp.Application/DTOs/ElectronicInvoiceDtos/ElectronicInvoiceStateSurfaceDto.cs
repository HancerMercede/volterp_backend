namespace Volterp.Application.DTOs.ElectronicInvoiceDtos;

// The surface the tenant sees: the count by state and the open documents.
public record ElectronicInvoiceStateSurfaceDto
{
    public ElectronicInvoiceStateCounts Counts { get; init; } = new();
    public IReadOnlyList<ElectronicInvoiceStateDto> Invoices { get; init; } = [];
}
