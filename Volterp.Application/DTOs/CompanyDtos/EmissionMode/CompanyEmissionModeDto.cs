using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.CompanyDtos.EmissionMode;

public record CompanyEmissionModeDto(
    CompanyEmissionMode EmissionMode,
    CompanyContingencyStatus ContingencyStatus,
    DateTime? ContingencyStartsAt,
    DateTime? ContingencyEndsAt);
