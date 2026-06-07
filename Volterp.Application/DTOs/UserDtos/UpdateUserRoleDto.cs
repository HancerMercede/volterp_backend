using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.UserDtos;


public record UpdateUserRoleDto(UserRole Role)
    :IMapTo<User>
{
    public User MapTo()
    {
        return new User
        {
            Role =  Role,
        };
    }
}
