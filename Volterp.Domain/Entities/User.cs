using System.ComponentModel.DataAnnotations;
using Volterp.Domain.Enums;

namespace Volterp.Domain.Entities;

public class User : AuditEntity
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    [EmailAddress(ErrorMessage =  "Invalid Email Address")]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "The Email address is not valid.")]
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Ventas;
    public bool IsActive { get; set; } = true;
    public int CompanyId { get; set; }
    
    public DateTime? LastLoginAt { get; set; }

    public Company Company { get; set; } = null!;
}