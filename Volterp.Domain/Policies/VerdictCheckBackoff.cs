namespace Volterp.Domain.Policies;

// How long to wait before asking the DGII again for the verdict of a
// document it is still validating. The DGII only says to wait "a prudent
// time" and does not fix the times, so they are ours: the wait grows with
// every check, it is dispersed so a batch of documents does not ask at the
// same instant, and it never passes the ceiling.
public sealed class VerdictCheckBackoff
{
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maximumDelay;
    private readonly double _jitterRatio;
    private readonly Func<double> _random;

    public VerdictCheckBackoff(TimeSpan initialDelay, TimeSpan maximumDelay, double jitterRatio, Func<double> random)
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

    public static VerdictCheckBackoff Default() => new(
        TimeSpan.FromMinutes(1),
        TimeSpan.FromHours(4),
        0.2,
        () => Random.Shared.NextDouble());

    // The wait that follows the checks a document already carries.
    public TimeSpan DelayFor(int checksAlreadyMade)
    {
        if (checksAlreadyMade < 0)
            throw new ArgumentOutOfRangeException(nameof(checksAlreadyMade));

        var exponential = _initialDelay.TotalMilliseconds * Math.Pow(2, Math.Min(checksAlreadyMade, 30));
        var capped = Math.Min(exponential, _maximumDelay.TotalMilliseconds);
        var band = capped * _jitterRatio;

        // The dispersion moves the wait inside its band, and the ceiling is a
        // ceiling: the wait never passes it.
        var delay = Math.Min(capped + band * (_random() * 2 - 1), _maximumDelay.TotalMilliseconds);

        return TimeSpan.FromMilliseconds(delay);
    }
}
