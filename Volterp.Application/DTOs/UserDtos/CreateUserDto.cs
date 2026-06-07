using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.UserDtos;

public record CreateUserDto(
    string Username,
    string Password,
    string Email,
    string FullName,
    UserRole Role,
    int CompanyId):IMapTo<User>
{
    public User MapTo()
    {
        return new User
        {
           Username =  Username,
           PasswordHash =  Password,
           Email =   Email,
           FullName =  FullName,
           Role =  Role,
           CompanyId =  CompanyId
        };
    }
}