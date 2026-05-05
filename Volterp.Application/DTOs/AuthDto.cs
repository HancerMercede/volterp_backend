namespace Volterp.Application.DTOs;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Username, string Email, string FullName, string Role, int CompanyId);
public record RegisterRequest(string Username, string Password, string Email, string FullName, int CompanyId, string? Role);
public record ErrorResponse(string Error, string? Details = null);