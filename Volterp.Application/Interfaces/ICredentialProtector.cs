namespace Volterp.Application.Interfaces;

public interface ICredentialProtector
{
    string Protect(Guid companyId, string plaintext);
    string Unprotect(Guid companyId, string protectedValue);
}
