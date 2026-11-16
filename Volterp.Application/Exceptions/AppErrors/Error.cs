namespace Volterp.Application.Exceptions.AppErrors;

public record Error(string Message, ErrorKind Kind = ErrorKind.Validation);