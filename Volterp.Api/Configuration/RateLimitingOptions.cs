using System.ComponentModel.DataAnnotations;

namespace Volterp.Api.Configuration;

public sealed class RateLimitingOptions : IValidatableObject
{
    public const string SectionName = "RateLimiting";

    public RateLimitPolicyOptions Authenticated { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };
    public RateLimitPolicyOptions Anonymous { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };
    public RateLimitPolicyOptions Authentication { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in ValidatePolicy(nameof(Authenticated), Authenticated))
            yield return result;
        foreach (var result in ValidatePolicy(nameof(Anonymous), Anonymous))
            yield return result;
        foreach (var result in ValidatePolicy(nameof(Authentication), Authentication))
            yield return result;
    }

    private static IEnumerable<ValidationResult> ValidatePolicy(string name, RateLimitPolicyOptions policy)
    {
        if (policy.PermitLimit < 1)
            yield return new ValidationResult(
                $"{name}.PermitLimit must be >= 1.",
                new[] { $"{name}.PermitLimit" });
        if (policy.WindowSeconds < 1)
            yield return new ValidationResult(
                $"{name}.WindowSeconds must be >= 1.",
                new[] { $"{name}.WindowSeconds" });
    }
}

public sealed class RateLimitPolicyOptions
{
    public int PermitLimit { get; set; } = 60;
    public int WindowSeconds { get; set; } = 60;
}