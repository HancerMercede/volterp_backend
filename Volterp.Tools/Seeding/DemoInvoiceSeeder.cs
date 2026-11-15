using EitherWay;
using Microsoft.EntityFrameworkCore;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;

namespace Volterp.Tools.Seeding;

public sealed class DemoInvoiceSeeder(IUnitOfWork unitOfWork, VolterpDbContext context) : IDemoInvoiceSeeder
{
    private static readonly DemoInvoiceDefinition[] Definitions =
    [
        new("DEMO-EC-PENDING", "E320000009001", DemoState.PendingEmission),
        new("DEMO-EC-IN-PROCESS", "E320000009002", DemoState.InProcess),
        new("DEMO-EC-REJECTED", "E320000009003", DemoState.Rejected),
        new("DEMO-EC-REVIEW", "E320000009004", DemoState.RequiresReview)
    ];

    public async Task<Either<Error, DemoInvoiceSeedOutcome>> SeedAsync(
        string taxId,
        CancellationToken ct = default)
    {
        if (!Rnc.TryParse(taxId, out var rnc))
            return Failure("The tax id must be a valid RNC or citizen ID.");

        var company = await unitOfWork.Companies.GetByCondictionsAsync(
            c => c.TaxId == rnc.Value,
            ct);

        if (company is null)
            return Failure($"There is no company with RNC {rnc.Value}.");

        var now = DateTime.UtcNow;
        var invoices = new List<ElectronicInvoice>();
        var created = 0;
        var existing = 0;

        foreach (var definition in Definitions)
        {
            var invoice = await FindInvoiceAsync(company, definition, ct);

            if (invoice is null)
            {
                invoice = await CreateInvoiceAsync(company, definition, now, ct);
                created++;
            }
            else
            {
                existing++;
            }

            invoices.Add(invoice);
        }

        if (created > 0)
            await unitOfWork.CommitAsync(ct);

        return Success(new DemoInvoiceSeedOutcome(
            company.Id,
            company.Name,
            company.TaxId,
            created,
            existing,
            Count(invoices, EcfFiscalState.PendingEmission),
            Count(invoices, EcfFiscalState.InProcess),
            Count(invoices, EcfFiscalState.Rejected),
            Count(invoices, EcfFiscalState.RequiresReview)));
    }

    // The tenant of the demo is the one the command was asked for, not the
    // one of the ambient context: the filter of the database belongs to the
    // request that is running, and the command is not a request of any
    // tenant. The company travels by hand and the filter is left out of
    // these two reads; it still guards every other query.
    private async Task<ElectronicInvoice?> FindInvoiceAsync(
        Company company,
        DemoInvoiceDefinition definition,
        CancellationToken ct)
        => await context.ElectronicInvoices
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.CompanyId == company.Id
                    && e.IssuerTaxId == company.TaxId
                    && e.ENCF == definition.Encf,
                ct);

    private async Task<Sale?> FindSaleAsync(
        Company company,
        DemoInvoiceDefinition definition,
        CancellationToken ct)
        => await context.Sales
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.CompanyId == company.Id && s.Code == definition.SaleCode,
                ct);

    private async Task<ElectronicInvoice> CreateInvoiceAsync(
        Company company,
        DemoInvoiceDefinition definition,
        DateTime now,
        CancellationToken ct)
    {
        var sale = await FindSaleAsync(company, definition, ct);

        if (sale is null)
        {
            sale = new Sale
            {
                Id = Guid.CreateVersion7(),
                CompanyId = company.Id,
                Code = definition.SaleCode,
                Status = SaleStatus.Completed,
                DocumentType = EcfDocumentType.Consumer,
                Subtotal = 100m,
                Itbis = 18m,
                Total = 118m,
                Notes = "Demo sale for the electronic invoice state banner."
            };

            await unitOfWork.Sales.AddSaleAsync(sale, ct);
        }

        var invoice = new ElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = company.Id,
            SaleId = sale.Id,
            ENCF = definition.Encf,
            DocumentType = EcfDocumentType.Consumer,
            IssuerTaxId = company.TaxId,
            Subtotal = 100m,
            Itbis = 18m,
            Total = 118m,
            SignedXml = string.Empty
        };

        ApplyState(invoice, definition.State, now);
        await unitOfWork.ElectronicInvoices.AddAsync(invoice, ct);

        return invoice;
    }

    private static void ApplyState(
        ElectronicInvoice invoice,
        DemoState state,
        DateTime now)
    {
        switch (state)
        {
            case DemoState.PendingEmission:
                // The demo has no certificate. The banner needs the fiscal
                // state, not a signature, so Sign is deliberately not called.
                invoice.Status = EcfStatus.Signed;
                invoice.ScheduleNextAttempt(now.AddYears(10));
                break;

            case DemoState.InProcess:
                invoice.Status = EcfStatus.Signed;
                invoice.MarkSent("demo-track-in-process", now);
                invoice.MarkInProcess();
                invoice.ScheduleNextCheck(now.AddYears(10));
                break;

            case DemoState.Rejected:
                invoice.Status = EcfStatus.Signed;
                invoice.MarkSent("demo-track-rejected", now);
                invoice.Reject("The DGII rejected this demo e-CF: the recipient RNC is invalid.");
                break;

            case DemoState.RequiresReview:
                invoice.MarkForReview(
                    "This demo e-CF could not be signed because the demo tenant has no certificate.");
                break;
        }
    }

    private static int Count(
        IEnumerable<ElectronicInvoice> invoices,
        EcfFiscalState state)
        => invoices.Count(invoice =>
            EcfFiscalStateClassifier.Classify(invoice.Status, invoice.RequiresReview) == state);

    private static Either<Error, DemoInvoiceSeedOutcome> Failure(string message)
        => new Either<Error, DemoInvoiceSeedOutcome>.Left(new Error(message));

    private static Either<Error, DemoInvoiceSeedOutcome> Success(DemoInvoiceSeedOutcome outcome)
        => new Either<Error, DemoInvoiceSeedOutcome>.Right(outcome);

    private sealed record DemoInvoiceDefinition(
        string SaleCode,
        string Encf,
        DemoState State);

    private enum DemoState
    {
        PendingEmission,
        InProcess,
        Rejected,
        RequiresReview
    }
}
