using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.CompanyDtos.EmissionMode;

public record UpdateCompanyContingencyDto(
    CompanyContingencyStatus Status,
    DateTime? StartsAt,
    DateTime? EndsAt);
