using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.UserDtos;

public record CreateUserDto(
    string Username,
    string Password,
    string Email,
    string FullName,
    UserRole Role):IMapTo<User>
{
    public User MapTo()
    {
        // CompanyId is NOT copied here — UserService.CreateAsync sets it from the
        // companyId parameter (which the controller reads from the JWT).
        return new User
        {
           Username =  Username,
           PasswordHash =  Password,
           Email =   Email,
           FullName =  FullName,
           Role =  Role
        };
    }
}