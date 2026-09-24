namespace Volterp.Api.Configuration;

public class JwtSettings
{
    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}

public class DatabaseSettings
{
    public string DefaultConnection { get; init; } = string.Empty;
}