namespace Volterp.Tools.Seeding;

public sealed record DemoInvoiceSeedOutcome(
    Guid CompanyId,
    string CompanyName,
    string TaxId,
    int CreatedCount,
    int ExistingCount,
    int PendingEmissionCount,
    int InProcessCount,
    int RejectedCount,
    int RequiresReviewCount);
