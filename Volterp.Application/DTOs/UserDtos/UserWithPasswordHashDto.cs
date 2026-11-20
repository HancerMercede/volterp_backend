using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.UserDtos;

public record UserWithPasswordHashDto: IMapFrom<User>
{
    public Guid Id { get; set; }
    // Optional: when it comes empty the service keeps the code the user already has.
    public string? Code { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    // The roles the user holds.
    public UserRole[] Roles { get; set; } = [];
    public bool IsActive { get; set; }
    public Guid CompanyId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;


    public void MapFrom(User source)
    {
        Id = source.Id;
        Code = source.Code;
        Username = source.Username;
        Email = source.Email;
        FullName = source.FullName;
        Roles = source.Roles.Select(assignment => assignment.Role).ToArray();
        IsActive = source.IsActive;
        CompanyId = source.CompanyId;
        PasswordHash = source.PasswordHash;
    }
};
