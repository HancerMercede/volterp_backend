namespace Volterp.DgiiSimulator;

public sealed class DgiiSimulatorOptions
{
    public const string SectionName = "DgiiSimulator";

    /// <summary>
    /// Where the simulator listens. Loopback by default: an emulator of the DGII
    /// that answers from the network is an invitation to use it for real.
    /// </summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:5090";

    /// <summary>
    /// The environment the simulator advertises. The path segment of every
    /// request is accepted as it comes, because the DGII's environments differ
    /// only by host.
    /// </summary>
    public string Environment { get; set; } = "testecf";

    /// <summary>The verdict a received e-CF gets when the request does not ask for another one.</summary>
    public SimulatorVerdict DefaultVerdict { get; set; } = SimulatorVerdict.Accepted;

    public string RejectionMessage { get; set; } = "El comprobante fue rechazado por el simulador.";

    public int RejectionCode { get; set; } = 1001;

    public string AcceptedMessage { get; set; } = "El comprobante fue recibido correctamente.";

    /// <summary>
    /// How long a document stays "En Proceso" before the verdict becomes final.
    /// Zero keeps it in process forever, which is what the DGII does while it
    /// validates a document.
    /// </summary>
    public int InProcessDurationMilliseconds { get; set; }

    /// <summary>The verdict a document ends with once its in-process window closes.</summary>
    public SimulatorVerdict VerdictAfterProcessing { get; set; } = SimulatorVerdict.Accepted;

    public int SeedDelayMilliseconds { get; set; }

    public int ReceptionDelayMilliseconds { get; set; }

    public int QueryDelayMilliseconds { get; set; }

    /// <summary>
    /// The share of requests the simulator answers with a server error, between
    /// zero and one. It is how the resilience of the client gets exercised
    /// without a real outage.
    /// </summary>
    public double FailureRate { get; set; }

    /// <summary>
    /// The only issuer RNC the simulator accepts documents from. Empty accepts
    /// every issuer.
    /// </summary>
    public string? AllowedIssuerRnc { get; set; }

    /// <summary>How long a seed stays valid.</summary>
    public int SeedLifetimeSeconds { get; set; } = 300;

    /// <summary>
    /// How long the token lives. The DGII grants one hour (p. 9 of the
    /// "Descripción Técnica Servicios DGII").
    /// </summary>
    public int TokenLifetimeMinutes { get; set; } = 60;
}

/// <summary>
/// The states the DGII publishes for a received document (p. 25 and p. 29 of
/// the "Descripción Técnica Servicios DGII"). The numbers are the DGII's, so
/// they are written down instead of being derived.
/// </summary>
public enum SimulatorVerdict
{
    NotFound = 0,
    Accepted = 1,
    Rejected = 2,
    InProcess = 3,
    AcceptedConditionally = 4
}
