using EitherWay;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

// The annulment of the ranges of e-NCF that the taxpayer will never use: the
// numbers that are left in a range have to be announced to the DGII or they
// are burned forever.
public interface IRangeAnnulmentService
{
    Task<Either<Error, RangeAnnulmentResult>> AnnulAsync(
        Guid sequenceId,
        Guid companyId,
        CancellationToken ct = default);
}

// What the DGII answered to the annulment of a range.
public record RangeAnnulmentResult(
    string? Code,
    string? Name,
    IReadOnlyList<string> Messages,
    DateTime? AnnulledAt);
