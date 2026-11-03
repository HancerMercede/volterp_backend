namespace Volterp.Domain.Abstractions;

public interface ITenantScoped
{
    Guid CompanyId { get; set; }
}
