using EitherWay;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Tools.Seeding;

public interface IDemoInvoiceSeeder
{
    Task<Either<Error, DemoInvoiceSeedOutcome>> SeedAsync(
        string taxId,
        CancellationToken ct = default);
}
