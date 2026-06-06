namespace Volterp.Application.DTOs;

public abstract record AuditableDto
{
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int? CreatedBy { get; init; }
    public int? UpdatedBy { get; init; }
}