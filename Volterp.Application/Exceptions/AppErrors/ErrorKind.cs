namespace Volterp.Application.Exceptions.AppErrors;

// The kind of the failure: the request broke a rule, or the thing the
// request asks for is not there. The status of the answer comes from here.
public enum ErrorKind
{
    Validation,
    NotFound
}
