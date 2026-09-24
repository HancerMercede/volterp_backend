using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.SupplierDtos;

/// <summary>
/// Cycle 1.1 wire refactor — request-only DTO for supplier updates.
///
/// Deliberately excludes <c>CompanyId</c>: tenant scope is decided exclusively by
/// the JWT-derived <c>companyId</c> on the server, never by anything the client
/// controls. See <see cref="SupplierDto"/> for the response shape (which still
/// carries <c>CompanyId</c> because the client UI needs to display it).
///
/// The wire-contract guard lives in <c>WireContractTests</c>.
/// </summary>
public record UpdateSupplierRequest
{
    public int Id { get; set; }

    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Category { get; set; }
    public string? ContactPerson { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? UpdatedAt { get; set; }
}
