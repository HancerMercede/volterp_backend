using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs;

public class CreateClientDto
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
    Guid? UpdatedBy
): IMapTo<Client>
{
    public Client MapTo()
    {
        return new Client
        {
            Name = Name,
            Email = Email,
            Phone = Phone,
            Address = Address,
            ImageUrl = ImageUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}