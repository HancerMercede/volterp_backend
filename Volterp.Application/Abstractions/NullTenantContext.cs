namespace Volterp.Application.Abstractions;

public sealed class NullTenantContext : ITenantContext
{
    public Guid? CompanyId => null;

    public bool IsUnrestricted => true;
}