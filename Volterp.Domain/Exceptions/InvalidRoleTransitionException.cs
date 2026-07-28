namespace Volterp.Domain.Exceptions;

public class InvalidRoleTransitionException : Exception
{
    public string FromRole { get; }
    public string ToRole { get; }

    public InvalidRoleTransitionException(string fromRole, string toRole)
        : base($"Invalid role transition: '{fromRole}' → '{toRole}'")
    {
        FromRole = fromRole;
        ToRole = toRole;
    }
}
