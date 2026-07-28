namespace Volterp.Domain.Exceptions;

public class InvalidEmailException : Exception
{
    public string? InvalidValue { get; }

    public InvalidEmailException(string? invalidValue)
        : base($"Invalid email: '{invalidValue}'")
    {
        InvalidValue = invalidValue;
    }
}
