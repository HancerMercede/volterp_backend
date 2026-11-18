using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;

namespace Volterp.Domain.Entities;

public class Company : AuditEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public string Address { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public CompanyEmissionMode EmissionMode { get; set; } = CompanyEmissionMode.Electronic;
    public CompanyContingencyStatus ContingencyStatus { get; set; } = CompanyContingencyStatus.None;
    public DateTime? ContingencyStartsAt { get; set; }
    public DateTime? ContingencyEndsAt { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();
    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public ICollection<AccountingTransaction> AccountingTransactions { get; set; } = new List<AccountingTransaction>();
    public ICollection<ElectronicInvoiceSequence> ElectronicInvoiceSequences { get; set; } = new List<ElectronicInvoiceSequence>();
    public ICollection<CodeSequence> CodeSequences { get; set; } = new List<CodeSequence>();
    public ICollection<CompanyCertificate> Certificates { get; set; } = new List<CompanyCertificate>();
    public ICollection<CompanyIssuer> Issuers { get; set; } = new List<CompanyIssuer>();
    public ICollection<ElectronicInvoice> ElectronicInvoices { get; set; } = new List<ElectronicInvoice>();

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    // The contingency is a window, not a mode: it suspends the electronic
    // path for a while and the company goes back to it when the window is
    // over. Without a contingency there are no bounds.
    public void DeclareContingency(CompanyContingencyStatus status, DateTime? startsAt, DateTime? endsAt)
    {
        var none = status == CompanyContingencyStatus.None;

        ContingencyStatus = status;
        ContingencyStartsAt = none ? null : startsAt;
        ContingencyEndsAt = none ? null : endsAt;
    }

    public void UpdateTaxId(Rnc rnc) => TaxId = rnc.Value;
    public void UpdateEmail(Email email) => Email = email.Value;
    public void UpdatePhone(string? phone) => Phone = phone ?? string.Empty;
}
