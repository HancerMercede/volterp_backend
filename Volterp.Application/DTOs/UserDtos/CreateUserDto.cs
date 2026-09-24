using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.UserDtos;

/// <summary>
/// Cycle 1.1 wire refactor — request-only DTO for users. Deliberately excludes
/// <c>CompanyId</c>: tenant scope is decided by the JWT-derived <c>companyId</c>
/// the controller passes to <c>UserService.CreateAsync</c>. The previous
/// <c>with &#123; CompanyId = companyId &#125;</c> patch at the controller layer
/// is no longer needed because the service sets the tenant explicitly.
/// Wire-contract guard lives in <c>WireContractTests</c>.
/// </summary>
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