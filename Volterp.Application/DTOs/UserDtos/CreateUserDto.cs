using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.UserDtos;

public record CreateUserDto(
    string Username,
    string Password,
    string Email,
    string FullName,
    UserRole[]? Roles):IMapTo<User>
{
    // Optional: when it comes empty the service asks the code generator for one.
    public string? Code { get; set; }

    public User MapTo()
    {
        // CompanyId is NOT copied here — UserService.CreateAsync sets it from the
        // companyId parameter (which the controller reads from the JWT).
        //
        // The roles are NOT copied here either: the assignment of a role
        // carries the company of the user, and the service assigns them once
        // the company is known.
        return new User
        {
           Username =  Username,
           PasswordHash =  Password,
           Email =   Email,
           FullName =  FullName,
           Code =  Code?.Trim() ?? string.Empty
        };
    }
}
