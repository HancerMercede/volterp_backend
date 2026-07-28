namespace Volterp.Application.Interfaces;

public interface ICurrentUserService
{
    bool IsSuperAdmin();
    int GetCompanyId();
    int? GetUserId();
}