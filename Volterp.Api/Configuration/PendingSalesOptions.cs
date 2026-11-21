using System.ComponentModel.DataAnnotations;

namespace Volterp.Api.Configuration;

// How long a pending sale waits for the customer, and how often the sweep
// looks for the ones nobody decided.
public sealed class PendingSalesOptions : IValidatableObject
{
    public const string SectionName = "PendingSales";

    // The lifetime of a pending sale, in hours: after it the sweep closes
    // the sale and the stock it reserved comes back.
    public int LifetimeHours { get; set; } = 24;

    // The interval between two sweeps, in minutes.
    public int SweepIntervalMinutes { get; set; } = 15;

    public TimeSpan Lifetime => TimeSpan.FromHours(LifetimeHours);

    public TimeSpan SweepInterval => TimeSpan.FromMinutes(SweepIntervalMinutes);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (LifetimeHours < 1)
            yield return new ValidationResult(
                $"{nameof(LifetimeHours)} must be >= 1.",
                new[] { nameof(LifetimeHours) });

        if (SweepIntervalMinutes < 1)
            yield return new ValidationResult(
                $"{nameof(SweepIntervalMinutes)} must be >= 1.",
                new[] { nameof(SweepIntervalMinutes) });
    }
}
