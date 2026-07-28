namespace Volterp.Domain.Exceptions;

public class InvalidSaleStateTransitionException : Exception
{
    public string FromStatus { get; }
    public string ToStatus { get; }

    public InvalidSaleStateTransitionException(string fromStatus, string toStatus)
        : base($"Invalid sale state transition: '{fromStatus}' → '{toStatus}'")
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }

    public InvalidSaleStateTransitionException(string fromStatus, string toStatus, string message)
        : base(message)
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }
}
