namespace Volterp.Domain.Exceptions;

public class InvalidElectronicInvoiceStateTransitionException : Exception
{
    public string FromStatus { get; }
    public string ToStatus { get; }

    public InvalidElectronicInvoiceStateTransitionException(string fromStatus, string toStatus)
        : base($"Invalid electronic invoice state transition: '{fromStatus}' → '{toStatus}'")
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }
}
