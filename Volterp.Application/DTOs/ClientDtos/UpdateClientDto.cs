using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs;

public record UpdateClientDto
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
):IMapTo<Client>
{
    public Client MapTo()
    {
        // Code is copied as requested; ClientService.UpdateClientAsync resolves it
        // (user code or the one the client already has) and validates its uniqueness.
        return new Client
        {
            Name = Name,
            Email = Email,
            Phone = Phone,
            Address = Address,
            TaxId = TaxId,
            ImageUrl = ImageUrl,
            IsActive = IsActive,
            Code = Code?.Trim() ?? string.Empty
        };
    }
}