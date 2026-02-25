namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Interface for predicting rewards for state-action-next state transitions.
/// Follows functional programming principles with async operations.
/// </summary>
public interface IRewardPredictor
{
    /// <summary>
    /// Predicts the reward for a transition.
    /// </summary>
    /// <param name="current">The current state.</param>
    /// <param name="action">The action taken.</param>
    /// <param name="next">The resulting next state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The predicted reward value.</returns>
    Task<double> PredictAsync(State current, Action action, State next, CancellationToken ct = default);
}