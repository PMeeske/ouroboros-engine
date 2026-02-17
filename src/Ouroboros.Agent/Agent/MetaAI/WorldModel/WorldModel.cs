namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Represents a learned world model for an environment domain.
/// Contains transition, reward, and terminal predictors.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="Id">Unique identifier for this model.</param>
/// <param name="Domain">The domain/environment this model represents.</param>
/// <param name="TransitionModel">Predictor for next state transitions.</param>
/// <param name="RewardModel">Predictor for rewards.</param>
/// <param name="TerminalModel">Predictor for terminal states.</param>
/// <param name="Hyperparameters">Model hyperparameters and configuration.</param>
public sealed record WorldModel(
    Guid Id,
    string Domain,
    IStatePredictor TransitionModel,
    IRewardPredictor RewardModel,
    ITerminalPredictor TerminalModel,
    Dictionary<string, object> Hyperparameters);