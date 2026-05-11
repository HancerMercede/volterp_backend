using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs;

public record UserDto(int Id, string Username, string Email, string FullName, UserRole Role, bool IsActive, int CompanyId);
public record CreateUserRequest(string Username, string Password, string Email, string FullName, UserRole Role, int CompanyId);
public record UpdateUserRoleRequest(UserRole Role);
public record UpdateUserStatusRequest(bool IsActive);
public record UserWithPasswordHashDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public int CompanyId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
};