namespace Volterp.Application.DTOs;

public record LoginRequest(string Username, string Password);

// The roles the user holds travel with the answer: the front shows what the
// user can do without asking the API again.
public record LoginResponse(string Token, string Username, string Email, string FullName, string[] Roles, Guid CompanyId);
public record ErrorResponse(string Error, string? Details = null);
