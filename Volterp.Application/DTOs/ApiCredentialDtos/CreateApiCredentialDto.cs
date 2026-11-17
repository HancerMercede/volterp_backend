namespace Volterp.Application.DTOs.ApiCredentialDtos;

// The request of the creation: the name the client gives to its credential
// and the day it stops working. The company is not here: it comes from the
// token of the caller.
public record CreateApiCredentialDto(string Name, DateTime? ExpiresAt);

// The answer of the creation: it carries the secret, and it is the only
// time the secret travels.
public record IssuedApiCredentialDto(
    Guid Id,
    string Name,
    string KeyId,
    string Secret,
    DateTime? ExpiresAt);
