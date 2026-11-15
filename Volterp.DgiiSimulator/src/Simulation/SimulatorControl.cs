namespace Volterp.DgiiSimulator;

/// <summary>
/// The headers that drive the simulator. They are not part of the DGII's
/// contract: they are the knobs a test turns to reproduce a condition the
/// simulator cannot be asked for over its own endpoints.
/// </summary>
internal static class SimulatorControl
{
    /// <summary>The verdict the received document gets: accepted, rejected, in-process, accepted-conditionally.</summary>
    public const string VerdictHeader = "X-DGII-Simulator-Verdict";

    /// <summary>The HTTP status the simulator answers with instead of doing its work.</summary>
    public const string ErrorHeader = "X-DGII-Simulator-Error";
}
