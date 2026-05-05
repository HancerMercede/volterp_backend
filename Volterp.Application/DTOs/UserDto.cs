namespace Volterp.Application.DTOs;

public record UserDto(int Id, string Username, string Email, string FullName, string Role, bool IsActive, int CompanyId);
public record CreateUserRequest(string Username, string Password, string Email, string FullName, string Role, int CompanyId);
public record UpdateUserRoleRequest(string Role);
public record UpdateUserStatusRequest(bool IsActive);