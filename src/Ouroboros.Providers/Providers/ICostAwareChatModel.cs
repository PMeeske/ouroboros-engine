namespace Ouroboros.Providers;

/// <summary>
/// Contract for models that support cost tracking.
/// </summary>
public interface ICostAwareChatModel : Ouroboros.Abstractions.Core.IChatCompletionModel
{
    /// <summary>
    /// Gets the cost tracker for this model instance.
    /// </summary>
    LlmCostTracker? CostTracker { get; }
}