using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.UserDtos;


public record UpdateUserStatusDto(bool IsActive):IMapTo<User>
{
    public User MapTo()
    {
        return new User
        {
            IsActive = IsActive
        };
    }
}