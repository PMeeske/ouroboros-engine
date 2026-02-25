namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Configuration for adaptive agent behavior.
/// </summary>
/// <param name="AdaptationThreshold">Minimum performance decline to trigger adaptation.</param>
/// <param name="RollbackThreshold">Performance decline after adaptation that triggers rollback.</param>
/// <param name="MinInteractionsBeforeAdaptation">Minimum interactions before allowing adaptation.</param>
/// <param name="EmaAlpha">Exponential moving average smoothing factor (0 &lt; alpha &lt;= 1).</param>
/// <param name="StagnationWindowSize">Window size for detecting stagnation.</param>
/// <param name="MaxAdaptationHistory">Maximum number of adaptation events to retain.</param>
public sealed record AdaptiveAgentConfig(
    double AdaptationThreshold = 0.1,
    double RollbackThreshold = 0.15,
    int MinInteractionsBeforeAdaptation = 50,
    double EmaAlpha = 0.1,
    int StagnationWindowSize = 20,
    int MaxAdaptationHistory = 100)
{
    /// <summary>
    /// Gets the default configuration with sensible defaults.
    /// </summary>
    public static AdaptiveAgentConfig Default => new();

    /// <summary>
    /// Configuration optimized for rapid adaptation.
    /// </summary>
    public static AdaptiveAgentConfig Aggressive => new(
        AdaptationThreshold: 0.05,
        RollbackThreshold: 0.1,
        MinInteractionsBeforeAdaptation: 20,
        EmaAlpha: 0.2,
        StagnationWindowSize: 10,
        MaxAdaptationHistory: 200);

    /// <summary>
    /// Configuration optimized for stability.
    /// </summary>
    public static AdaptiveAgentConfig Conservative => new(
        AdaptationThreshold: 0.2,
        RollbackThreshold: 0.25,
        MinInteractionsBeforeAdaptation: 100,
        EmaAlpha: 0.05,
        StagnationWindowSize: 50,
        MaxAdaptationHistory: 50);
}