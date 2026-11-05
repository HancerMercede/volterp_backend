using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.UserDtos;

public record UserDto:IMapFrom<User>
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Username  { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName  { get; set; } = string.Empty;
    public UserRole Role  { get; set; }
    public bool IsActive  { get; set; }
    public Guid CompanyId  { get; set; }

    public void MapFrom(User source)
    {
        Id = source.Id;
        Code = source.Code;
        Username = source.Username;
        Email = source.Email;
        FullName = source.FullName;
        Role = source.Role;
        IsActive = source.IsActive;
        CompanyId = source.CompanyId;
    }
}
