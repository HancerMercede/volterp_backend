namespace Volterp.Application.DTOs.SeedDtos;

// The answer of the validation of a signed seed. The names are the ones of
// the DGII, so the third party reads the same three fields it reads against
// the authority.
public sealed record SeedValidationDto(string Token, string Expira, string Expedido);
