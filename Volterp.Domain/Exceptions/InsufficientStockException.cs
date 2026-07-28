namespace Volterp.Domain.Exceptions;

public class InsufficientStockException : Exception
{
    public int Requested { get; }
    public int Available { get; }

    public InsufficientStockException(int requested, int available)
        : base($"Insufficient stock. Requested: {requested}, Available: {available}")
    {
        Requested = requested;
        Available = available;
    }
}
