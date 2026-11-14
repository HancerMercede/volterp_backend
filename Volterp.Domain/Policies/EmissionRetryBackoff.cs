namespace Volterp.Domain.Policies;

// How long to wait before sending a document again. The wait grows with every
// attempt the document already had, it is dispersed so a batch of documents
// does not hit the DGII at the same instant, and it never passes the ceiling.
public sealed class EmissionRetryBackoff
{
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maximumDelay;
    private readonly double _jitterRatio;
    private readonly Func<double> _random;

    public EmissionRetryBackoff(TimeSpan initialDelay, TimeSpan maximumDelay, double jitterRatio, Func<double> random)
    {
        if (initialDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(initialDelay), "The first wait has to be a positive one.");

        if (maximumDelay < initialDelay)
            throw new ArgumentOutOfRangeException(nameof(maximumDelay), "The ceiling cannot be below the first wait.");

        if (jitterRatio is < 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(jitterRatio), "The dispersion has to be a fraction of the wait.");

        ArgumentNullException.ThrowIfNull(random);

        _initialDelay = initialDelay;
        _maximumDelay = maximumDelay;
        _jitterRatio = jitterRatio;
        _random = random;
    }

    public static EmissionRetryBackoff Default() => new(
        TimeSpan.FromMinutes(1),
        TimeSpan.FromHours(4),
        0.2,
        () => Random.Shared.NextDouble());

    // The wait that follows the attempts a document already had.
    public TimeSpan DelayFor(int attemptsAlreadyMade)
    {
        if (attemptsAlreadyMade < 0)
            throw new ArgumentOutOfRangeException(nameof(attemptsAlreadyMade));

        var exponential = _initialDelay.TotalMilliseconds * Math.Pow(2, Math.Min(attemptsAlreadyMade, 30));
        var capped = Math.Min(exponential, _maximumDelay.TotalMilliseconds);
        var band = capped * _jitterRatio;

        // The dispersion moves the wait inside its band, and the ceiling is a
        // ceiling: the wait never passes it.
        var delay = Math.Min(capped + band * (_random() * 2 - 1), _maximumDelay.TotalMilliseconds);

        return TimeSpan.FromMilliseconds(delay);
    }
}
