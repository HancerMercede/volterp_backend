namespace Volterp.Application.Abstractions;

public interface ITenantContext
{
    Guid? CompanyId { get; }
}