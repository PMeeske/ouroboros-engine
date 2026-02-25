namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Interface for predicting whether a state is terminal (episode ending).
/// Follows functional programming principles with async operations.
/// </summary>
public interface ITerminalPredictor
{
    /// <summary>
    /// Predicts whether the given state is terminal.
    /// </summary>
    /// <param name="state">The state to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the state is terminal, false otherwise.</returns>
    Task<bool> PredictAsync(State state, CancellationToken ct = default);
}