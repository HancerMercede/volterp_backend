using System.Collections.Concurrent;
using System.Globalization;

namespace Volterp.DgiiSimulator;

/// <summary>
/// The seeds the simulator hands out. A seed is worth nothing until it comes
/// back signed inside the file the taxpayer sends, so what is remembered is
/// the value and the moment it was issued.
/// </summary>
internal sealed class SimulatorSeeds(DgiiSimulatorOptions options)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _issued = new();

    public string Issue()
    {
        var value = Guid.NewGuid().ToString("N");
        _issued[value] = DateTimeOffset.UtcNow;
        return value;
    }

    public bool IsIssued(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!_issued.TryGetValue(value, out var issuedAt))
            return false;

        return DateTimeOffset.UtcNow - issuedAt <= TimeSpan.FromSeconds(options.SeedLifetimeSeconds);
    }

    /// <summary>
    /// The shape of the seed (p. 10 of the "Descripción Técnica Servicios
    /// DGII"): a SemillaModel with the value and the moment it was generated.
    /// </summary>
    public string BuildXml(string value) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
        + "<SemillaModel xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\""
        + " xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">"
        + $"<valor>{value}</valor>"
        + $"<fecha>{DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture)}</fecha>"
        + "</SemillaModel>";
}
