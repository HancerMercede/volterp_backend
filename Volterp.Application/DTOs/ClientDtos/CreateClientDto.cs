using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.ClientDtos;

public record CreateClientDto
(
    string Name,
    string Email,
    string Phone,
    string Address,
    string? ImageUrl,
    bool IsActive,
    DateTime? CreatedAt,
    DateTime? UpdatedAt,
    Guid? CreatedBy,
    Guid? UpdatedBy,
    string? TaxId = null,
    string? Code = null
): IMapTo<Client>
{
    public Client MapTo()
    {
        // CompanyId is NOT copied here — ClientService.CreateClientAsync sets
        // it from the companyId parameter (which the controller reads from the JWT).
        // Code is copied as requested; ClientService.CreateClientAsync resolves it
        // (user code or generated sequence) and validates its uniqueness.
        return new Client
        {
            Name = Name,
            Email = Email,
            Phone = Phone,
            Address = Address,
            TaxId = TaxId,
            ImageUrl = ImageUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Code = Code?.Trim() ?? string.Empty
        };
    }
}