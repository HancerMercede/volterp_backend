namespace Volterp.Domain.Exceptions;

public class InvalidTaxIdException : Exception
{
    public string? InvalidValue { get; }

    public InvalidTaxIdException(string? invalidValue)
        : base($"Invalid Tax ID: '{invalidValue}'")
    {
        InvalidValue = invalidValue;
    }
}
