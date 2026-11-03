namespace Volterp.Domain.Exceptions;

public class InvalidRncException : Exception
{
    public string? InvalidValue { get; }

    public InvalidRncException(string? invalidValue)
        : base($"Invalid RNC: '{invalidValue}'. Expected a 9-digit company RNC or an 11-digit citizen ID.")
    {
        InvalidValue = invalidValue;
    }
}
