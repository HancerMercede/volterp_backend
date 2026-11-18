using EitherWay;
using Volterp.Application.DTOs.SeedDtos;
using Volterp.Application.Exceptions.AppErrors;

namespace Volterp.Application.Interfaces;

public interface ISeedAuthenticationService
{
    Task<Either<Error, SeedDto>> IssueSeedAsync(CancellationToken ct = default);

    Task<Either<Error, SeedValidationDto>> ValidateAsync(string xml, CancellationToken ct = default);
}

// The token of every third party that validated a seed. It is keyed by the
// subject of the certificate that signed the seed, because the token belongs
// to the signer and not to a tenant of ours, and it lives until it expires.
public interface ISeedTokenCache
{
    Task<SeedSession> GetOrIssueAsync(
        string subjectName,
        Func<SeedSession> issue,
        CancellationToken ct = default);
}
