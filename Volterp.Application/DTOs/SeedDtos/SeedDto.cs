namespace Volterp.Application.DTOs.SeedDtos;

// The seed the platform issues. The names of the fields are the ones the DGII
// uses in its own answer, because that is the contract the third party codes
// against.
public sealed record SeedDto(string Valor, DateTimeOffset Fecha);

// The session of the third party that validated a seed: the token it holds,
// the moment it was issued and the moment it dies.
public sealed record SeedSession(
    string SubjectName,
    string Token,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
