namespace Ouroboros.Agent;

/// <summary>
/// Builder for creating orchestrated chat models with fluent configuration.
/// </summary>
public sealed class OrchestratorBuilder
{
    private readonly SmartModelOrchestrator _orchestrator;
    private readonly List<(ModelCapability, Ouroboros.Abstractions.Core.IChatCompletionModel)> _models = new();
    private bool _trackMetrics = true;

    public OrchestratorBuilder(ToolRegistry baseTools, string fallbackModel = "default")
    {
        _orchestrator = new SmartModelOrchestrator(baseTools, fallbackModel);
    }

    /// <summary>
    /// Registers a model with its capabilities.
    /// </summary>
    public OrchestratorBuilder WithModel(
        string name,
        Ouroboros.Abstractions.Core.IChatCompletionModel model,
        ModelType type,
        string[] strengths,
        int maxTokens = 4096,
        double avgCost = 1.0,
        double avgLatencyMs = 1000.0)
    {
        ModelCapability capability = new ModelCapability(
            name,
            strengths,
            maxTokens,
            avgCost,
            avgLatencyMs,
            type);

        _models.Add((capability, model));
        return this;
    }

    /// <summary>
    /// Enables or disables performance metric tracking.
    /// </summary>
    public OrchestratorBuilder WithMetricTracking(bool enabled)
    {
        _trackMetrics = enabled;
        return this;
    }

    /// <summary>
    /// Builds the orchestrated chat model.
    /// </summary>
    public OrchestratedChatModel Build()
    {
        // Register all models
        foreach ((ModelCapability capability, Ouroboros.Abstractions.Core.IChatCompletionModel model) in _models)
        {
            _orchestrator.RegisterModel(capability, model);
        }

        return new OrchestratedChatModel(_orchestrator, _trackMetrics);
    }

    /// <summary>
    /// Gets the underlying orchestrator for advanced scenarios.
    /// </summary>
    public IModelOrchestrator GetOrchestrator() => _orchestrator;
}