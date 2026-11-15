using System.Collections.Concurrent;

namespace Volterp.DgiiSimulator;

/// <summary>
/// The e-CFs the simulator received, with the verdict each one carries.
/// </summary>
internal sealed class SimulatorDocuments
{
    private readonly ConcurrentDictionary<string, SimulatorDocument> _byTrackId = new();
    private readonly List<SimulatorDocument> _received = [];

    public SimulatorDocument Add(SimulatorDocument document)
    {
        _byTrackId[document.TrackId] = document;

        lock (_received)
        {
            _received.Add(document);
        }

        return document;
    }

    public SimulatorDocument? FindByTrackId(string? trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            return null;

        return _byTrackId.GetValueOrDefault(trackId);
    }

    /// <summary>
    /// The consultation by e-NCF only answers the session that sent the
    /// document. The DGII asks the caller to be delegated for the emitter or
    /// the buyer (p. 26 and p. 30 of the "Descripción Técnica Servicios DGII"),
    /// and the session is what the simulator has instead of a delegation.
    /// </summary>
    public SimulatorDocument? FindByEncf(string sessionId, string? issuerRnc, string? encf)
    {
        if (string.IsNullOrWhiteSpace(issuerRnc) || string.IsNullOrWhiteSpace(encf))
            return null;

        lock (_received)
        {
            for (var index = _received.Count - 1; index >= 0; index--)
            {
                var document = _received[index];
                if (document.SessionId == sessionId
                    && string.Equals(document.IssuerRnc, issuerRnc, StringComparison.Ordinal)
                    && string.Equals(document.Encf, encf, StringComparison.OrdinalIgnoreCase))
                {
                    return document;
                }
            }
        }

        return null;
    }
}

/// <summary>
/// A received e-CF and what the DGII thinks of it.
/// </summary>
internal sealed class SimulatorDocument
{
    public required string TrackId { get; init; }

    public required string SessionId { get; init; }

    public string? IssuerRnc { get; init; }

    public string? Encf { get; init; }

    public string? BuyerRnc { get; init; }

    public string? ForeignId { get; init; }

    public decimal? TotalAmount { get; init; }

    public decimal? TotalItbis { get; init; }

    public string? IssueDate { get; init; }

    public string? SignedAt { get; init; }

    public string? SecurityCode { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public required SimulatorVerdict Verdict { get; init; }

    /// <summary>When the verdict stops being "En Proceso" and becomes final.</summary>
    public DateTimeOffset? VerdictAt { get; init; }

    /// <summary>
    /// The verdict the document carries right now. A document under
    /// validation is in process until its window closes.
    /// </summary>
    public SimulatorVerdict CurrentVerdict(DateTimeOffset now)
    {
        if (Verdict == SimulatorVerdict.InProcess && VerdictAt is { } final && now >= final)
            return FinalVerdict;

        return Verdict;
    }

    public SimulatorVerdict FinalVerdict { get; init; } = SimulatorVerdict.Accepted;
}
